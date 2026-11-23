using System.Net;
using Google;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Application.Options;
using GumpStudio.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GumpStudio.Infrastructure.GoogleCalendar;

/// <summary>
/// Sistema → Google Agenda (seção 16, MVP 8). OAuth2 com refresh token do estúdio guardado criptografado
/// (Data Protection). Os eventos usam o id da sessão como EventId, então reenvios nunca duplicam.
/// </summary>
public sealed class GoogleCalendarGateway(IAppDbContext db, IOptions<GoogleOptions> options,
    IDataProtectionProvider dataProtection, TimeProvider clock, ILogger<GoogleCalendarGateway> logger)
    : ICalendarGateway, ICalendarConnectionService
{
    private const string TokenPurpose = "GumpStudio.GoogleCalendar.RefreshToken";
    private const string StatePurpose = "GumpStudio.GoogleCalendar.OAuthState";
    private static readonly string[] Scopes = [CalendarService.Scope.CalendarEvents, "openid", "email"];

    private GoogleOptions Opt => options.Value;

    // ---------- Conexão (OAuth) ----------

    public async Task<CalendarStatusDto> GetStatusAsync(CancellationToken ct)
    {
        var c = await db.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(ct);
        return c is null ? new CalendarStatusDto(false, null, null, null)
                         : new CalendarStatusDto(true, c.GoogleAccountEmail, c.CalendarId, c.ConnectedAt);
    }

    public string BuildAuthorizationUrl(Guid adminUserId)
    {
        if (string.IsNullOrWhiteSpace(Opt.ClientId) || string.IsNullOrWhiteSpace(Opt.CalendarRedirectUri))
            throw new InvalidInputException("Google OAuth não configurado (Google:ClientId / Google:CalendarRedirectUri).");

        var state = dataProtection.CreateProtector(StatePurpose).ToTimeLimitedDataProtector()
            .Protect(adminUserId.ToString(), TimeSpan.FromMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = Opt.ClientId,
            ["redirect_uri"] = Opt.CalendarRedirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', Scopes),
            ["access_type"] = "offline",     // necessário para receber refresh token
            ["prompt"] = "consent",          // garante o refresh token mesmo em reconexões
            ["state"] = state
        };
        return "https://accounts.google.com/o/oauth2/v2/auth?" +
               string.Join('&', query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task ConnectAsync(string code, string state, CancellationToken ct)
    {
        Guid adminId;
        try
        {
            var raw = dataProtection.CreateProtector(StatePurpose).ToTimeLimitedDataProtector().Unprotect(state, out _);
            adminId = Guid.Parse(raw);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            throw new InvalidInputException("Estado do OAuth inválido ou expirado. Inicie a conexão novamente.");
        }

        var token = await CreateFlow().ExchangeCodeForTokenAsync("studio", code, Opt.CalendarRedirectUri, ct);
        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new InvalidInputException(
                "O Google não devolveu o refresh token. Remova o acesso do app em myaccount.google.com/permissions e conecte novamente.");

        var email = "desconhecido";
        try
        {
            if (!string.IsNullOrEmpty(token.IdToken))
                email = (await GoogleJsonWebSignature.ValidateAsync(token.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { Opt.ClientId } })).Email;
        }
        catch (InvalidJwtException ex) { logger.LogWarning(ex, "Não foi possível ler o e-mail da conta Google conectada"); }

        var protectedToken = dataProtection.CreateProtector(TokenPurpose).Protect(token.RefreshToken);
        var now = clock.GetUtcNow();
        var existing = await db.GoogleConnections.FirstOrDefaultAsync(ct);
        if (existing is null)
            db.GoogleConnections.Add(StudioGoogleConnection.Create(email, protectedToken, Opt.CalendarId, now, adminId));
        else
            existing.Reconnect(email, protectedToken, now, adminId);
        await db.SaveChangesAsync(ct);
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        var conn = await db.GoogleConnections.FirstOrDefaultAsync(ct);
        if (conn is null) return;
        try
        {
            var refresh = dataProtection.CreateProtector(TokenPurpose).Unprotect(conn.ProtectedRefreshToken);
            await CreateFlow().RevokeTokenAsync("studio", refresh, ct);   // melhor esforço
        }
        catch (Exception ex) { logger.LogWarning(ex, "Não foi possível revogar o token no Google"); }

        db.Remove(conn);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Eventos ----------

    public async Task<bool> IsConnectedAsync(CancellationToken ct) =>
        await db.GoogleConnections.AsNoTracking().AnyAsync(ct);

    public async Task<string> UpsertEventAsync(CalendarEventData data, CancellationToken ct)
    {
        var (service, calendarId) = await CreateServiceAsync(ct);
        using (service)
        {
            var ev = new Event
            {
                Id = data.EventId,
                Status = "confirmed",   // "ressuscita" um evento cancelado com o mesmo id
                Summary = data.Title,
                Description = data.Description,
                Location = data.Location,
                Start = new EventDateTime { DateTimeDateTimeOffset = data.Start, TimeZone = Opt.TimeZone },
                End = new EventDateTime { DateTimeDateTimeOffset = data.End, TimeZone = Opt.TimeZone }
            };

            try
            {
                return (await service.Events.Insert(ev, calendarId).ExecuteAsync(ct)).Id;
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.Conflict)
            {
                // Já existe: atualiza em vez de duplicar.
                return (await service.Events.Update(ev, calendarId, data.EventId).ExecuteAsync(ct)).Id;
            }
        }
    }

    public async Task CancelEventAsync(string eventId, CancellationToken ct)
    {
        var (service, calendarId) = await CreateServiceAsync(ct);
        using (service)
        {
            try { await service.Events.Delete(calendarId, eventId).ExecuteAsync(ct); }
            catch (GoogleApiException e) when (e.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // já removido: idempotente
            }
        }
    }

    // ---------- Infra ----------

    private GoogleAuthorizationCodeFlow CreateFlow() => new(new GoogleAuthorizationCodeFlow.Initializer
    {
        ClientSecrets = new ClientSecrets { ClientId = Opt.ClientId, ClientSecret = Opt.ClientSecret },
        Scopes = Scopes
    });

    private async Task<(CalendarService Service, string CalendarId)> CreateServiceAsync(CancellationToken ct)
    {
        var conn = await db.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(ct)
                   ?? throw new InvalidOperationException("Google Agenda não conectado.");
        var refresh = dataProtection.CreateProtector(TokenPurpose).Unprotect(conn.ProtectedRefreshToken);
        var credential = new UserCredential(CreateFlow(), "studio", new TokenResponse { RefreshToken = refresh });
        var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "GumpStudio"
        });
        return (service, conn.CalendarId);
    }
}
