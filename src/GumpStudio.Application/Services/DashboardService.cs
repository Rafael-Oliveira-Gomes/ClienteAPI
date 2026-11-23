using GumpStudio.Application.Abstractions;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Application.Services;

public sealed record DashboardSessionDto(Guid Id, string ClientName, string ArtistName, DateTimeOffset StartsAt, SessionStatus Status);

public sealed record DashboardDto(
    string Role,
    int SessionsToday,
    int UpcomingSessions,
    int LowStockCount,
    decimal? MonthIncome, decimal? MonthExpense, decimal? MonthBalance,
    int? ActiveArtists,
    int? ClientsCount,
    IReadOnlyList<DashboardSessionDto> NextSessions,
    IReadOnlyList<StockAlertDto> LowStock,
    IReadOnlyList<MovementDto> LatestMovements);

/// <summary>Dashboards do administrador e do tatuador (seção 31).</summary>
public sealed class DashboardService(IAppDbContext db, ICurrentUser current, StockService stock, TimeProvider clock)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var sessions = db.TattooSessions.AsNoTracking()
            .Where(s => s.Status != SessionStatus.Cancelled && s.Status != SessionStatus.NoShow);
        if (!current.IsAdmin()) { var artistId = current.RequireArtistId(); sessions = sessions.Where(s => s.ArtistId == artistId); }

        var today = await sessions.CountAsync(s => s.StartsAt >= dayStart && s.StartsAt < dayEnd, ct);
        var upcoming = await sessions.CountAsync(s => s.StartsAt >= now, ct);
        var next = await (from s in sessions.Where(s => s.StartsAt >= now).OrderBy(s => s.StartsAt).Take(10)
                          join c in db.Clients on s.ClientId equals c.Id
                          join a in db.TattooArtists on s.ArtistId equals a.Id
                          orderby s.StartsAt
                          select new DashboardSessionDto(s.Id, c.Name, a.Name, s.StartsAt, s.Status)).ToListAsync(ct);

        var alerts = await stock.LowStockAlertsAsync(ct);
        var latest = await stock.HistoryAsync(null, null, null, null, null, 10, ct);

        if (!current.IsAdmin())
            return new DashboardDto("Artist", today, upcoming, alerts.Count, null, null, null, null, null, next, alerts, latest);

        var monthStart = DateOnly.FromDateTime(new DateTime(now.Year, now.Month, 1));
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var tx = await db.FinancialTransactions.AsNoTracking().Where(t => t.Date >= monthStart && t.Date <= monthEnd)
            .GroupBy(t => t.Type).Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) }).ToListAsync(ct);
        var income = tx.FirstOrDefault(t => t.Type == TransactionType.Income)?.Total ?? 0m;
        var expense = tx.FirstOrDefault(t => t.Type == TransactionType.Expense)?.Total ?? 0m;
        var activeArtists = await db.TattooArtists.CountAsync(a => a.IsActive, ct);
        var clients = await db.Clients.CountAsync(ct);

        return new DashboardDto("Admin", today, upcoming, alerts.Count, income, expense, income - expense,
            activeArtists, clients, next, alerts, latest);
    }
}
