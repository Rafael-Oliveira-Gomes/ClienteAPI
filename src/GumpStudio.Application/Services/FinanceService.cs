using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Application.Services;

public sealed record TransactionRequest(TransactionType Type, string Description, decimal Amount, DateOnly Date,
    string Category, Guid? SessionId, Guid? ArtistId, Guid? ClientId, string? Notes);

public sealed record TransactionDto(Guid Id, TransactionType Type, string Description, decimal Amount, DateOnly Date,
    string Category, Guid? SessionId, Guid? ArtistId, Guid? ClientId, string? Notes, Guid? ReversesTransactionId,
    DateTimeOffset CreatedAt, Guid? CreatedBy);

public sealed record FinanceSummaryDto(DateOnly From, DateOnly To, decimal Income, decimal Expense, decimal Balance,
    int CompletedSessions, decimal SessionsReceived, IReadOnlyList<CategoryTotalDto> ByCategory);

public sealed record CategoryTotalDto(string Category, TransactionType Type, decimal Total);

/// <summary>Financeiro básico baseado em lançamentos imutáveis (seção 15 e 42). Somente administrador.</summary>
public sealed class FinanceService(IAppDbContext db, ICurrentUser current, TimeProvider clock)
{
    public async Task<IReadOnlyList<TransactionDto>> ListAsync(DateOnly? from, DateOnly? to, TransactionType? type,
        string? category, Guid? artistId, Guid? sessionId, CancellationToken ct)
    {
        current.EnsureAdmin();
        var q = db.FinancialTransactions.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(t => t.Date >= from);
        if (to.HasValue) q = q.Where(t => t.Date <= to);
        if (type.HasValue) q = q.Where(t => t.Type == type);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(t => t.Category == category);
        if (artistId.HasValue) q = q.Where(t => t.ArtistId == artistId);
        if (sessionId.HasValue) q = q.Where(t => t.SessionId == sessionId);
        var rows = await q.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).Take(500).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<TransactionDto> CreateAsync(TransactionRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        var t = FinancialTransaction.Create(r.Type, r.Description, r.Amount, r.Date, r.Category, r.SessionId, r.ArtistId, r.ClientId, r.Notes);
        db.FinancialTransactions.Add(t);
        await db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    /// <summary>Estorno = novo lançamento negativo; o original nunca é alterado nem apagado.</summary>
    public async Task<TransactionDto> ReverseAsync(Guid id, string? notes, CancellationToken ct)
    {
        current.EnsureAdmin();
        var original = await db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == id, ct)
                       ?? throw new NotFoundException("Lançamento não encontrado.");
        if (await db.FinancialTransactions.AnyAsync(t => t.ReversesTransactionId == id, ct))
            throw new ConflictException("Este lançamento já foi estornado.");

        var reversal = FinancialTransaction.Reverse(original, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), notes);
        db.FinancialTransactions.Add(reversal);
        await db.SaveChangesAsync(ct);
        return ToDto(reversal);
    }

    public async Task<FinanceSummaryDto> SummaryAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        current.EnsureAdmin();
        if (to < from) throw new InvalidInputException("Período inválido.");

        var totals = await db.FinancialTransactions.AsNoTracking().Where(t => t.Date >= from && t.Date <= to)
            .GroupBy(t => new { t.Category, t.Type })
            .Select(g => new CategoryTotalDto(g.Key.Category, g.Key.Type, g.Sum(t => t.Amount))).ToListAsync(ct);

        var income = totals.Where(t => t.Type == TransactionType.Income).Sum(t => t.Total);
        var expense = totals.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Total);

        var fromDt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toDt = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var completed = await db.TattooSessions.AsNoTracking()
            .Where(s => s.Status == SessionStatus.Completed && s.CompletedAt >= fromDt && s.CompletedAt < toDt)
            .Select(s => s.Value).ToListAsync(ct);

        return new FinanceSummaryDto(from, to, income, expense, income - expense, completed.Count, completed.Sum(), totals);
    }

    internal static TransactionDto ToDto(FinancialTransaction t) => new(t.Id, t.Type, t.Description, t.Amount, t.Date,
        t.Category, t.SessionId, t.ArtistId, t.ClientId, t.Notes, t.ReversesTransactionId, t.CreatedAt, t.CreatedBy);
}
