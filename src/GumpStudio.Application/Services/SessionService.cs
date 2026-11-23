using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GumpStudio.Application.Services;

public sealed record CreateSessionRequest(Guid ClientId, Guid? ArtistId, DateTimeOffset StartsAt, int DurationMinutes,
    decimal Value, string? Location, string? Notes, string? ReferenceUrl);

public sealed record UpdateSessionRequest(Guid? ArtistId, DateTimeOffset StartsAt, int DurationMinutes,
    decimal Value, string? Location, string? Notes, string? ReferenceUrl);

public sealed record CancelSessionRequest(string? Reason);

public sealed record CompleteSessionRequest(bool RegisterIncome = true, string? PaymentCategory = null);

public sealed record SessionDto(Guid Id, Guid ClientId, string ClientName, Guid ArtistId, string ArtistName,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, int DurationMinutes, decimal Value, SessionStatus Status,
    string? Location, string? Notes, string? ReferenceUrl, string? GoogleCalendarEventId,
    CalendarSyncStatus CalendarSyncStatus, string? CalendarSyncError, DateTimeOffset? CompletedAt);

public sealed record SessionDetailDto(SessionDto Session, IReadOnlyList<MovementDto> Usage, IReadOnlyList<Guid> DocumentIds);

public sealed class SessionService(IAppDbContext db, ICurrentUser current, TimeProvider clock,
    ICalendarGateway calendar, ILogger<SessionService> logger)
{
    // ---------- Consulta ----------

    public async Task<IReadOnlyList<SessionDto>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? artistId,
        Guid? clientId, SessionStatus? status, CancellationToken ct)
    {
        var q = db.TattooSessions.AsNoTracking().AsQueryable();
        if (!current.IsAdmin()) { var own = current.RequireArtistId(); q = q.Where(s => s.ArtistId == own); }
        else if (artistId.HasValue) q = q.Where(s => s.ArtistId == artistId);
        if (clientId.HasValue) q = q.Where(s => s.ClientId == clientId);
        if (status.HasValue) q = q.Where(s => s.Status == status);
        if (from.HasValue) q = q.Where(s => s.StartsAt >= from);
        if (to.HasValue) q = q.Where(s => s.StartsAt <= to);
        var sessions = await q.OrderBy(s => s.StartsAt).Take(500).ToListAsync(ct);
        return await ToDtosAsync(sessions, ct);
    }

    public async Task<SessionDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await LoadReadable(id, ct);
        var dto = (await ToDtosAsync([s], ct))[0];
        var usage = await db.StockMovements.AsNoTracking().Where(m => m.SessionId == id).OrderBy(m => m.CreatedAt).ToListAsync(ct);
        var docs = await db.ExamDocuments.AsNoTracking().Where(d => d.SessionId == id).Select(d => d.Id).ToListAsync(ct);
        return new SessionDetailDto(dto,
            usage.Select(m => new MovementDto(m.Id, m.StockId, m.ProductId, m.Type, m.Quantity, m.QuantityDelta, m.UnitCost,
                m.UnitSalePrice, m.SessionId, m.InvoiceId, m.ReversesMovementId, m.Reason, m.Notes, m.UserId, m.CreatedAt)).ToList(),
            docs);
    }

    // ---------- Comandos ----------

    public async Task<SessionDto> CreateAsync(CreateSessionRequest r, CancellationToken ct)
    {
        var artistId = ResolveArtistId(r.ArtistId);
        await EnsureClientVisible(r.ClientId, ct);
        await EnsureArtistAvailable(artistId, ct);

        var session = TattooSession.Create(r.ClientId, artistId, r.StartsAt, r.DurationMinutes, r.Value, r.Location, r.Notes, r.ReferenceUrl);
        await EnsureNoOverlap(session, null, ct);

        db.TattooSessions.Add(session);
        await db.SaveChangesAsync(ct);

        await SyncCalendarAsync(session, ct);   // melhor esforço: falha do Google não derruba a sessão
        return (await ToDtosAsync([session], ct))[0];
    }

    public async Task<SessionDto> UpdateAsync(Guid id, UpdateSessionRequest r, CancellationToken ct)
    {
        var session = await LoadWritable(id, ct);
        var artistId = current.IsAdmin() ? (r.ArtistId ?? session.ArtistId) : session.ArtistId;
        if (artistId != session.ArtistId) await EnsureArtistAvailable(artistId, ct);

        session.Reschedule(artistId, r.StartsAt, r.DurationMinutes);
        session.SetDetails(r.Value, r.Location, r.Notes, r.ReferenceUrl);
        await EnsureNoOverlap(session, id, ct);
        await db.SaveChangesAsync(ct);

        await SyncCalendarAsync(session, ct);
        return (await ToDtosAsync([session], ct))[0];
    }

    public Task<SessionDto> ConfirmAsync(Guid id, CancellationToken ct) => TransitionAsync(id, s => s.Confirm(), ct);
    public Task<SessionDto> StartAsync(Guid id, CancellationToken ct) => TransitionAsync(id, s => s.Start(), ct);

    public async Task<SessionDto> MarkNoShowAsync(Guid id, CancellationToken ct)
    {
        var dto = await TransitionAsync(id, s => s.MarkNoShow(), ct);
        return dto;
    }

    public async Task<SessionDto> CancelAsync(Guid id, CancelSessionRequest r, CancellationToken ct)
    {
        var session = await LoadWritable(id, ct);
        session.Cancel(r.Reason);
        await db.SaveChangesAsync(ct);

        if (session.GoogleCalendarEventId is { } eventId)
        {
            try { await calendar.CancelEventAsync(eventId, ct); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao cancelar evento {EventId} no Google Agenda", eventId);
                session.MarkCalendarFailed("Falha ao cancelar evento: " + ex.Message);
                await db.SaveChangesAsync(ct);
            }
        }
        return (await ToDtosAsync([session], ct))[0];
    }

    /// <summary>
    /// Conclui a sessão e registra o recebimento (fluxo da seção 8). O uso de produtos já deve ter
    /// sido registrado em /stock-movements/usage — o estoque é debitado nesse momento, não aqui.
    /// </summary>
    public async Task<SessionDto> CompleteAsync(Guid id, CompleteSessionRequest r, CancellationToken ct)
    {
        var session = await LoadWritable(id, ct);
        session.Complete(clock.GetUtcNow());

        if (r.RegisterIncome && session.Value > 0)
        {
            var alreadyRegistered = await db.FinancialTransactions.AnyAsync(
                t => t.SessionId == id && t.Type == TransactionType.Income && t.ReversesTransactionId == null, ct);
            if (!alreadyRegistered)
            {
                var clientName = await db.Clients.Where(c => c.Id == session.ClientId).Select(c => c.Name).FirstAsync(ct);
                db.FinancialTransactions.Add(FinancialTransaction.Create(TransactionType.Income,
                    $"Tatuagem - {clientName}", session.Value, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
                    string.IsNullOrWhiteSpace(r.PaymentCategory) ? "Tatuagem" : r.PaymentCategory,
                    session.Id, session.ArtistId, session.ClientId, null));
            }
        }

        await db.SaveChangesAsync(ct);   // status + financeiro na mesma transação
        return (await ToDtosAsync([session], ct))[0];
    }

    /// <summary>Reenvia a sessão ao Google Agenda (após falha ou conexão tardia).</summary>
    public async Task<SessionDto> ResyncCalendarAsync(Guid id, CancellationToken ct)
    {
        var session = await LoadWritableAny(id, ct);
        await SyncCalendarAsync(session, ct);
        return (await ToDtosAsync([session], ct))[0];
    }

    // ---------- Google Agenda ----------

    private async Task SyncCalendarAsync(TattooSession session, CancellationToken ct)
    {
        try
        {
            if (!await calendar.IsConnectedAsync(ct)) return;   // sem conexão: segue NotSynced

            var client = await db.Clients.AsNoTracking().FirstAsync(c => c.Id == session.ClientId, ct);
            var artist = await db.TattooArtists.AsNoTracking().FirstAsync(a => a.Id == session.ArtistId, ct);
            var description =
                $"Cliente: {client.Name}\nTatuador: {artist.Name}\nTelefone: {client.WhatsApp ?? client.Phone ?? "-"}\n" +
                $"Duração: {session.EstimatedDurationMinutes} min\nObservações: {session.Notes ?? "-"}";

            // EventId determinístico (id da sessão): reenvios atualizam o mesmo evento, sem duplicar.
            var eventId = await calendar.UpsertEventAsync(new CalendarEventData(
                session.Id.ToString("N"), $"Tattoo • {client.Name} ({artist.Name})", description,
                session.StartsAt, session.EndsAt, session.Location), ct);
            session.MarkCalendarSynced(eventId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao sincronizar sessão {SessionId} com o Google Agenda", session.Id);
            session.MarkCalendarFailed(ex.Message);
        }
        await db.SaveChangesAsync(ct);
    }

    // ---------- Helpers ----------

    private async Task<SessionDto> TransitionAsync(Guid id, Action<TattooSession> action, CancellationToken ct)
    {
        var session = await LoadWritable(id, ct);
        action(session);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([session], ct))[0];
    }

    private Guid ResolveArtistId(Guid? requested)
    {
        if (!current.IsAdmin()) return current.RequireArtistId();   // tatuador só agenda para si
        return requested ?? throw new InvalidInputException("Informe o tatuador.");
    }

    private async Task EnsureClientVisible(Guid clientId, CancellationToken ct)
    {
        var exists = await db.Clients.AnyAsync(c => c.Id == clientId, ct);
        if (!exists) throw new NotFoundException("Cliente não encontrado.");
    }

    private async Task EnsureArtistAvailable(Guid artistId, CancellationToken ct)
    {
        var artist = await db.TattooArtists.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artistId, ct)
                     ?? throw new NotFoundException("Tatuador não encontrado.");
        if (!artist.IsActive) throw new ConflictException("Tatuador desativado.");
    }

    private async Task EnsureNoOverlap(TattooSession s, Guid? ignoreId, CancellationToken ct)
    {
        var overlap = await db.TattooSessions.AnyAsync(x =>
            x.ArtistId == s.ArtistId && x.Id != ignoreId &&
            x.Status != SessionStatus.Cancelled && x.Status != SessionStatus.NoShow &&
            x.StartsAt < s.EndsAt && x.EndsAt > s.StartsAt, ct);
        if (overlap) throw new ConflictException("O tatuador já possui uma sessão nesse horário.");
    }

    private async Task<TattooSession> LoadReadable(Guid id, CancellationToken ct)
    {
        var s = await db.TattooSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Sessão não encontrada.");
        if (!current.IsAdmin() && s.ArtistId != current.RequireArtistId())
            throw new ForbiddenException("Esta sessão pertence a outro tatuador.");
        return s;
    }

    private async Task<TattooSession> LoadWritableAny(Guid id, CancellationToken ct)
    {
        var s = await db.TattooSessions.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Sessão não encontrada.");
        if (!current.IsAdmin() && s.ArtistId != current.RequireArtistId())
            throw new ForbiddenException("Esta sessão pertence a outro tatuador.");
        return s;
    }

    private Task<TattooSession> LoadWritable(Guid id, CancellationToken ct) => LoadWritableAny(id, ct);

    private async Task<IReadOnlyList<SessionDto>> ToDtosAsync(IReadOnlyCollection<TattooSession> sessions, CancellationToken ct)
    {
        var clientIds = sessions.Select(s => s.ClientId).Distinct().ToList();
        var artistIds = sessions.Select(s => s.ArtistId).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var artists = await db.TattooArtists.AsNoTracking().Where(a => artistIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        return sessions.Select(s => new SessionDto(s.Id, s.ClientId, clients.GetValueOrDefault(s.ClientId, "?"),
            s.ArtistId, artists.GetValueOrDefault(s.ArtistId, "?"), s.StartsAt, s.EndsAt, s.EstimatedDurationMinutes,
            s.Value, s.Status, s.Location, s.Notes, s.ReferenceUrl, s.GoogleCalendarEventId, s.CalendarSyncStatus,
            s.CalendarSyncError, s.CompletedAt)).ToList();
    }
}
