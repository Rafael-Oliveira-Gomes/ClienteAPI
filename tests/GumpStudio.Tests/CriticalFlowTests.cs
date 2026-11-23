using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Application.Options;
using GumpStudio.Application.Services;
using GumpStudio.Domain.Common;
using GumpStudio.Domain.Enums;
using GumpStudio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GumpStudio.Tests;

internal sealed class TestUser : ICurrentUser
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public UserRole? Role { get; set; } = UserRole.Admin;
    public Guid? ArtistId { get; set; }
    public string? IpAddress => "127.0.0.1";
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeCalendar : ICalendarGateway
{
    public bool Connected { get; set; } = true;
    public bool Fail { get; set; }
    public List<CalendarEventData> Upserts { get; } = [];
    public List<string> Cancelled { get; } = [];

    public Task<bool> IsConnectedAsync(CancellationToken ct) => Task.FromResult(Connected);

    public Task<string> UpsertEventAsync(CalendarEventData data, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("Google fora do ar");
        Upserts.Add(data);
        return Task.FromResult(data.EventId);
    }

    public Task CancelEventAsync(string eventId, CancellationToken ct) { Cancelled.Add(eventId); return Task.CompletedTask; }
}

/// <summary>
/// Fluxo crítico da seção 34: criar sessão → registrar uso → reduzir estoque → criar financeiro → concluir.
/// Usa EF InMemory; o provedor real (PostgreSQL) só é exercitado no ambiente com Docker.
/// </summary>
public class CriticalFlowTests
{
    private readonly TestUser _user = new();
    private readonly FixedTime _clock = new(new DateTimeOffset(2026, 11, 23, 15, 0, 0, TimeSpan.Zero));
    private readonly FakeCalendar _calendar = new();
    private readonly AppDbContext _db;

    private readonly ArtistService _artists;
    private readonly ClientService _clients;
    private readonly ProductService _products;
    private readonly StockService _stock;
    private readonly SessionService _sessions;
    private readonly FinanceService _finance;

    public CriticalFlowTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(options, _user, _clock);
        _artists = new ArtistService(_db, _user, _clock);
        _clients = new ClientService(_db, _user);
        _products = new ProductService(_db, _user);
        _stock = new StockService(_db, _user);
        _sessions = new SessionService(_db, _user, _clock, _calendar, NullLogger<SessionService>.Instance);
        _finance = new FinanceService(_db, _user, _clock);
    }

    private void ActAsAdmin() { _user.Role = UserRole.Admin; _user.ArtistId = null; _user.UserId = Guid.NewGuid(); }
    private void ActAsArtist(ArtistDto a) { _user.Role = UserRole.Artist; _user.ArtistId = a.Id; _user.UserId = a.UserId; }

    private async Task<(ArtistDto Artist, StockDto Stock, ProductDto Product, ClientDto Client)> SeedAsync()
    {
        var artist = await _artists.CreateAsync(new CreateArtistRequest("João", "joao@gmail.com", null, null, null,
            ArtistType.Fixed, new DateOnly(2026, 11, 1), null,
            new CompensationInput(CompensationType.Percentage, null, null, 60m, null), null), default);
        var stock = await _stock.CreateAsync(new StockRequest("Estoque João", false, artist.Id), default);
        var product = await _products.CreateAsync(new ProductRequest("Tinta preta", UnitOfMeasure.Ml, "TP-1", 4.5m, 10m, 5m), default);
        var client = await _clients.CreateAsync(new ClientRequest("Ana", null, "11999990000", null, null, null, null, null), default);
        await _stock.RegisterEntryAsync(new EntryRequest(stock.Id, product.Id, 10, 4.5m, null, null, null), default);
        return (artist, stock, product, client);
    }

    [Fact]
    public async Task Sessao_uso_estoque_financeiro_e_conclusao()
    {
        var (artist, stock, product, client) = await SeedAsync();

        // Tatuador agenda para si; evento é criado no Google Agenda.
        ActAsArtist(artist);
        var session = await _sessions.CreateAsync(new CreateSessionRequest(client.Id, null,
            _clock.GetUtcNow().AddDays(1), 120, 500m, "Sala 1", "Braço", null), default);
        Assert.Equal(artist.Id, session.ArtistId);
        Assert.Equal(CalendarSyncStatus.Synced, session.CalendarSyncStatus);
        Assert.Equal(session.Id.ToString("N"), _calendar.Upserts.Single().EventId);

        // Uso reduz o estoque (10 → 7).
        await _stock.RegisterUsageAsync(new UsageRequest(session.Id, stock.Id, product.Id, 3, 4.5m, null, null), default);
        var balance = (await _stock.BalancesAsync(stock.Id, default)).Single(b => b.ProductId == product.Id);
        Assert.Equal(7m, balance.Balance);
        Assert.False(balance.BelowMinimum);

        // Saída maior que o saldo é recusada (RN01) e nada é gravado.
        await Assert.ThrowsAsync<DomainException>(() =>
            _stock.RegisterUsageAsync(new UsageRequest(session.Id, stock.Id, product.Id, 8, 4.5m, null, null), default));
        Assert.Equal(7m, (await _stock.BalancesAsync(stock.Id, default)).Single(b => b.ProductId == product.Id).Balance);

        // Concluir: confirma → inicia → conclui, gerando a entrada financeira.
        await _sessions.ConfirmAsync(session.Id, default);
        await _sessions.StartAsync(session.Id, default);
        var done = await _sessions.CompleteAsync(session.Id, new CompleteSessionRequest(), default);
        Assert.Equal(SessionStatus.Completed, done.Status);

        ActAsAdmin();
        var tx = Assert.Single(await _finance.ListAsync(null, null, TransactionType.Income, null, null, session.Id, default));
        Assert.Equal(500m, tx.Amount);

        // Depois de concluída não aceita novo uso.
        ActAsArtist(artist);
        await Assert.ThrowsAsync<ConflictException>(() =>
            _stock.RegisterUsageAsync(new UsageRequest(session.Id, stock.Id, product.Id, 1, 4.5m, null, null), default));
    }

    [Fact]
    public async Task Estoque_abaixo_do_minimo_gera_alerta()
    {
        var (artist, stock, product, client) = await SeedAsync();
        ActAsArtist(artist);
        var session = await _sessions.CreateAsync(new CreateSessionRequest(client.Id, null, _clock.GetUtcNow().AddDays(1), 60, 100m, null, null, null), default);

        await _stock.RegisterUsageAsync(new UsageRequest(session.Id, stock.Id, product.Id, 6, 4.5m, null, null), default);   // sobra 4 < 5

        var alert = Assert.Single(await _stock.LowStockAlertsAsync(default));
        Assert.Equal(4m, alert.Balance);
    }

    [Fact]
    public async Task Tatuador_nao_movimenta_estoque_sem_permissao_RN07()
    {
        var (artist, _, product, _) = await SeedAsync();
        ActAsAdmin();
        var outro = await _stock.CreateAsync(new StockRequest("Estoque Principal", true, null), default);
        await _stock.RegisterEntryAsync(new EntryRequest(outro.Id, product.Id, 5, 4.5m, null, null, null), default);

        ActAsArtist(artist);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _stock.RegisterDamagedAsync(new DamagedRequest(outro.Id, product.Id, 1, "quebrou", null), default));
    }

    [Fact]
    public async Task Tatuador_nao_usa_sessao_de_outro_tatuador()
    {
        var (artist, stock, product, client) = await SeedAsync();
        ActAsArtist(artist);
        var session = await _sessions.CreateAsync(new CreateSessionRequest(client.Id, null, _clock.GetUtcNow().AddDays(1), 60, 100m, null, null, null), default);

        ActAsAdmin();
        var maria = await _artists.CreateAsync(new CreateArtistRequest("Maria", "maria@gmail.com", null, null, null,
            ArtistType.Fixed, new DateOnly(2026, 11, 1), null, new CompensationInput(CompensationType.Daily, null, 150m, null, null), [stock.Id]), default);

        ActAsArtist(maria);
        await Assert.ThrowsAsync<ForbiddenException>(() => _sessions.GetAsync(session.Id, default));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _stock.RegisterUsageAsync(new UsageRequest(session.Id, stock.Id, product.Id, 1, 4.5m, null, null), default));
    }

    [Fact]
    public async Task Sessoes_do_mesmo_tatuador_nao_podem_se_sobrepor()
    {
        var (artist, _, _, client) = await SeedAsync();
        ActAsArtist(artist);
        var start = _clock.GetUtcNow().AddDays(1);
        await _sessions.CreateAsync(new CreateSessionRequest(client.Id, null, start, 120, 100m, null, null, null), default);

        await Assert.ThrowsAsync<ConflictException>(() => _sessions.CreateAsync(
            new CreateSessionRequest(client.Id, null, start.AddMinutes(60), 60, 100m, null, null, null), default));
    }

    [Fact]
    public async Task Falha_do_google_nao_impede_a_sessao_e_pode_ser_reenviada()
    {
        var (artist, _, _, client) = await SeedAsync();
        ActAsArtist(artist);
        _calendar.Fail = true;

        var session = await _sessions.CreateAsync(new CreateSessionRequest(client.Id, null, _clock.GetUtcNow().AddDays(1), 60, 100m, null, null, null), default);
        Assert.Equal(CalendarSyncStatus.Failed, session.CalendarSyncStatus);

        _calendar.Fail = false;
        var retried = await _sessions.ResyncCalendarAsync(session.Id, default);
        Assert.Equal(CalendarSyncStatus.Synced, retried.CalendarSyncStatus);
    }

    [Fact]
    public async Task Cancelar_remove_o_evento_do_google()
    {
        var (artist, _, _, client) = await SeedAsync();
        ActAsArtist(artist);
        var session = await _sessions.CreateAsync(new CreateSessionRequest(client.Id, null, _clock.GetUtcNow().AddDays(1), 60, 100m, null, null, null), default);

        await _sessions.CancelAsync(session.Id, new CancelSessionRequest("cliente desistiu"), default);
        Assert.Contains(session.Id.ToString("N"), _calendar.Cancelled);
    }

    [Fact]
    public async Task Movimentacao_de_estoque_e_imutavel_correcao_cria_nova_movimentacao()
    {
        var (_, stock, product, _) = await SeedAsync();
        ActAsAdmin();
        var entry = (await _stock.HistoryAsync(stock.Id, product.Id, null, null, null, 10, default)).Single();

        var correction = await _stock.CorrectAsync(entry.Id, new CorrectionRequest("lançado em duplicidade"), default);
        Assert.Equal(StockMovementType.CorrectionExit, correction.Type);
        Assert.Equal(0m, (await _stock.BalancesAsync(stock.Id, default)).Single(b => b.ProductId == product.Id).Balance);
        await Assert.ThrowsAsync<ConflictException>(() => _stock.CorrectAsync(entry.Id, new CorrectionRequest("de novo"), default));

        // UPDATE/DELETE físico é bloqueado pelo DbContext.
        var tracked = await _db.StockMovements.FirstAsync(m => m.Id == entry.Id);
        _db.StockMovements.Remove(tracked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Auditoria_registra_usuario_responsavel()
    {
        var (_, stock, product, _) = await SeedAsync();
        var movement = (await _stock.HistoryAsync(stock.Id, product.Id, null, null, null, 1, default)).Single();
        Assert.Equal(_user.UserId, movement.UserId);
        Assert.Equal(_clock.GetUtcNow(), movement.CreatedAt);
    }
}

public class AuthTests
{
    private sealed class FakeGoogle : IGoogleIdTokenValidator
    {
        // idToken no formato "email|subject" para simplificar.
        public Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct)
        {
            var parts = idToken.Split('|');
            if (parts.Length != 2) throw new UnauthorizedException("inválido");
            return Task.FromResult(new GoogleIdentity(parts[1], parts[0], true, "Fulano"));
        }
    }

    private sealed class FakeTokens : ITokenService
    {
        public AccessToken CreateAccessToken(GumpStudio.Domain.Entities.User user, Guid? artistId) =>
            new("jwt-" + user.Email, DateTimeOffset.UtcNow.AddMinutes(30));
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N");
        public string HashRefreshToken(string refreshToken) => "h:" + refreshToken;
    }

    private static readonly FixedTime Clock = new(new DateTimeOffset(2026, 11, 23, 15, 0, 0, TimeSpan.Zero));

    private static (AuthService Auth, AppDbContext Db, TestUser User) Build(string? bootstrap)
    {
        var user = new TestUser();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, user, Clock);
        var auth = new AuthService(db, new FakeGoogle(), new FakeTokens(), Clock,
            Options.Create(new JwtOptions()), Options.Create(new AuthOptions { BootstrapAdminEmail = bootstrap }));
        return (auth, db, user);
    }

    [Fact]
    public async Task Primeiro_admin_entra_pelo_email_de_bootstrap()
    {
        var (auth, _, _) = Build("dono@gmail.com");
        var result = await auth.LoginWithGoogleAsync("dono@gmail.com|g-1", default);
        Assert.Equal(UserRole.Admin, result.User.Role);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
    }

    [Fact]
    public async Task Email_nao_cadastrado_e_recusado()
    {
        var (auth, _, _) = Build("dono@gmail.com");
        await Assert.ThrowsAsync<ForbiddenException>(() => auth.LoginWithGoogleAsync("intruso@gmail.com|g-9", default));
    }

    [Fact]
    public async Task Conta_google_diferente_da_vinculada_e_recusada()
    {
        var (auth, _, _) = Build("dono@gmail.com");
        await auth.LoginWithGoogleAsync("dono@gmail.com|g-1", default);
        await Assert.ThrowsAsync<ForbiddenException>(() => auth.LoginWithGoogleAsync("dono@gmail.com|OUTRA", default));
    }

    [Fact]
    public async Task Tatuador_rotativo_expirado_nao_entra()
    {
        var (auth, db, user) = Build("dono@gmail.com");
        await auth.LoginWithGoogleAsync("dono@gmail.com|g-1", default);

        var artists = new ArtistService(db, user, Clock);
        await artists.CreateAsync(new CreateArtistRequest("Visitante", "visita@gmail.com", null, null, null, ArtistType.Rotating,
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 10),   // terminou há 13 dias
            new CompensationInput(CompensationType.Daily, null, 150m, null, null), null), default);

        await Assert.ThrowsAsync<AccessDeniedException>(() => auth.LoginWithGoogleAsync("visita@gmail.com|g-2", default));
    }

    [Fact]
    public async Task Refresh_token_e_rotativo_e_nao_pode_ser_reutilizado()
    {
        var (auth, _, _) = Build("dono@gmail.com");
        var first = await auth.LoginWithGoogleAsync("dono@gmail.com|g-1", default);

        var second = await auth.RefreshAsync(first.RefreshToken, default);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync(first.RefreshToken, default));
    }
}
