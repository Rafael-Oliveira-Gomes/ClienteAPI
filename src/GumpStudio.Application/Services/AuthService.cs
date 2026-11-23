using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Application.Options;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GumpStudio.Application.Services;

public sealed record UserDto(Guid Id, string Email, string Name, UserRole Role, bool IsActive,
    Guid? ArtistId, DateTimeOffset? AccessStartsAt, DateTimeOffset? AccessExpiresAt, AccessStatus AccessStatus,
    DateTimeOffset? LastLoginAt);

public sealed record AuthResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, UserDto User);

/// <summary>Login por e-mail Google (ID token) + JWT próprio e refresh token rotativo.</summary>
public sealed class AuthService(IAppDbContext db, IGoogleIdTokenValidator google, ITokenService tokens,
    TimeProvider clock, IOptions<JwtOptions> jwt, IOptions<AuthOptions> auth)
{
    public async Task<AuthResult> LoginWithGoogleAsync(string idToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idToken)) throw new InvalidInputException("idToken é obrigatório.");

        var identity = await google.ValidateAsync(idToken, ct);
        if (!identity.EmailVerified) throw new UnauthorizedException("E-mail Google não verificado.");
        return await SignInAsync(identity.Email, identity.Name, identity.Subject, ct);
    }

    /// <summary>
    /// SOMENTE DESENVOLVIMENTO (o endpoint só é mapeado em Development): entra só com o e-mail, sem Google.
    /// Respeita as mesmas regras (usuário cadastrado, ativo, janela de acesso) e não vincula conta Google.
    /// </summary>
    public Task<AuthResult> DevLoginAsync(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new InvalidInputException("E-mail é obrigatório.");
        return SignInAsync(email, null, null, ct);
    }

    private async Task<AuthResult> SignInAsync(string rawEmail, string? name, string? subject, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var email = rawEmail.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            // Bootstrap: o primeiro administrador é definido por configuração.
            var bootstrap = auth.Value.BootstrapAdminEmail?.Trim().ToLowerInvariant();
            if (bootstrap == email && !await db.Users.AnyAsync(ct))
            {
                user = User.Create(email, name ?? email, UserRole.Admin);
                db.Users.Add(user);
            }
            else
            {
                throw new ForbiddenException("Este e-mail não está cadastrado. Peça a um administrador para liberar seu acesso.");
            }
        }

        if (subject is not null && user.GoogleSubject is not null && user.GoogleSubject != subject)
            throw new ForbiddenException("Conta Google diferente da cadastrada para este usuário.");

        user.EnsureCanAuthenticate(now);   // bloqueia inativo / rotativo expirado
        user.RegisterLogin(subject, now);

        return await IssueAsync(user, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new InvalidInputException("refreshToken é obrigatório.");
        var now = clock.GetUtcNow();
        var hash = tokens.HashRefreshToken(refreshToken);

        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || !stored.IsActive(now)) throw new UnauthorizedException("Sessão expirada. Faça login novamente.");

        var user = await db.Users.FirstAsync(u => u.Id == stored.UserId, ct);
        user.EnsureCanAuthenticate(now);

        stored.Revoke(now);   // rotação
        return await IssueAsync(user, ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return;
        stored.Revoke(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("Usuário não encontrado.");
        var artistId = await db.TattooArtists.Where(a => a.UserId == userId).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        return UserMapping.ToDto(user, artistId, clock.GetUtcNow());
    }

    private async Task<AuthResult> IssueAsync(User user, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var artistId = await db.TattooArtists.Where(a => a.UserId == user.Id)
            .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);

        var access = tokens.CreateAccessToken(user, artistId);
        var refresh = tokens.GenerateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Create(user.Id, tokens.HashRefreshToken(refresh), now,
            TimeSpan.FromDays(jwt.Value.RefreshTokenDays)));
        await db.SaveChangesAsync(ct);

        return new AuthResult(access.Token, access.ExpiresAt, refresh, UserMapping.ToDto(user, artistId, now));
    }
}

public static class UserMapping
{
    public static UserDto ToDto(User u, Guid? artistId, DateTimeOffset now) =>
        new(u.Id, u.Email, u.Name, u.Role, u.IsActive, artistId, u.AccessStartsAt, u.AccessExpiresAt,
            u.GetAccessStatus(now), u.LastLoginAt);
}
