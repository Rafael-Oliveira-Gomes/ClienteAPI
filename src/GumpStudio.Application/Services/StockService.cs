using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using GumpStudio.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Application.Services;

public sealed record StockRequest(string Name, bool IsMain, Guid? OwnerArtistId);
public sealed record StockUpdateRequest(string Name, bool IsActive);
public sealed record StockDto(Guid Id, string Name, bool IsMain, Guid? OwnerArtistId, bool IsActive);

public sealed record StockBalanceDto(Guid StockId, Guid ProductId, string ProductName, UnitOfMeasure Unit,
    decimal Balance, decimal MinimumStock, bool BelowMinimum);

public sealed record StockAlertDto(Guid StockId, string StockName, Guid ProductId, string ProductName,
    UnitOfMeasure Unit, decimal Balance, decimal MinimumStock);

public sealed record EntryRequest(Guid StockId, Guid ProductId, decimal Quantity, decimal? UnitCost,
    decimal? UnitSalePrice, Guid? InvoiceId, string? Notes);
public sealed record UsageRequest(Guid SessionId, Guid StockId, Guid ProductId, decimal Quantity, decimal? UnitCost,
    decimal? UnitSalePrice, string? Notes);
public sealed record DamagedRequest(Guid StockId, Guid ProductId, decimal Quantity, string Reason, string? Notes);
public sealed record AdjustmentRequest(Guid StockId, Guid ProductId, decimal QuantityToReduce, string Reason, string? Notes);
public sealed record CorrectionRequest(string Reason);

public sealed record MovementDto(Guid Id, Guid StockId, Guid ProductId, StockMovementType Type, decimal Quantity,
    decimal QuantityDelta, decimal? UnitCost, decimal? UnitSalePrice, Guid? SessionId, Guid? InvoiceId,
    Guid? ReversesMovementId, string? Reason, string? Notes, Guid UserId, DateTimeOffset CreatedAt);

/// <summary>
/// Estoque baseado em movimentações (seção 42). Nunca altera "quantidade do produto":
/// o saldo é a soma das movimentações do par (estoque, produto).
/// </summary>
public sealed class StockService(IAppDbContext db, ICurrentUser current)
{
    // ---------- Estoques ----------

    public async Task<IReadOnlyList<StockDto>> ListAsync(CancellationToken ct)
    {
        var q = db.Stocks.AsNoTracking().AsQueryable();
        if (!current.IsAdmin())
        {
            var allowed = await AllowedStockIdsAsync(ct);
            q = q.Where(s => allowed.Contains(s.Id) && s.IsActive);
        }
        return (await q.OrderByDescending(s => s.IsMain).ThenBy(s => s.Name).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<StockDto> CreateAsync(StockRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        if (r.OwnerArtistId.HasValue && !await db.TattooArtists.AnyAsync(a => a.Id == r.OwnerArtistId, ct))
            throw new InvalidInputException("Tatuador responsável não encontrado.");
        if (r.IsMain && await db.Stocks.AnyAsync(s => s.IsMain && s.IsActive, ct))
            throw new ConflictException("Já existe um estoque principal ativo.");

        var stock = Stock.Create(r.Name, r.IsMain, r.OwnerArtistId);
        db.Stocks.Add(stock);

        // O dono do estoque recebe permissão de uso automaticamente.
        if (r.OwnerArtistId.HasValue)
            db.ArtistStockAccesses.Add(new ArtistStockAccess(r.OwnerArtistId.Value, stock.Id));

        await db.SaveChangesAsync(ct);
        return ToDto(stock);
    }

    public async Task<StockDto> UpdateAsync(Guid id, StockUpdateRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Estoque não encontrado.");
        stock.Update(r.Name, r.IsActive);
        await db.SaveChangesAsync(ct);
        return ToDto(stock);
    }

    // ---------- Saldos e alertas ----------

    public async Task<IReadOnlyList<StockBalanceDto>> BalancesAsync(Guid stockId, CancellationToken ct)
    {
        await EnsureCanViewStockAsync(stockId, ct);
        var balances = await db.StockMovements.AsNoTracking().Where(m => m.StockId == stockId)
            .GroupBy(m => m.ProductId).Select(g => new { ProductId = g.Key, Balance = g.Sum(m => m.QuantityDelta) })
            .ToListAsync(ct);
        var withHistory = balances.Select(b => b.ProductId).ToList();
        var products = await db.Products.AsNoTracking().Where(p => p.IsActive || withHistory.Contains(p.Id))
            .OrderBy(p => p.Name).ToListAsync(ct);

        return products.Select(p =>
        {
            var balance = balances.FirstOrDefault(b => b.ProductId == p.Id)?.Balance ?? 0m;
            return new StockBalanceDto(stockId, p.Id, p.Name, p.Unit, balance, p.MinimumStock,
                StockRules.IsBelowMinimum(balance, p.MinimumStock));
        }).ToList();
    }

    /// <summary>Produtos abaixo do mínimo nos estoques que o usuário pode ver (seção 13).</summary>
    public async Task<IReadOnlyList<StockAlertDto>> LowStockAlertsAsync(CancellationToken ct)
    {
        var stocks = db.Stocks.AsNoTracking().Where(s => s.IsActive);
        if (!current.IsAdmin())
        {
            var allowed = await AllowedStockIdsAsync(ct);
            stocks = stocks.Where(s => allowed.Contains(s.Id));
        }
        var stockList = await stocks.ToListAsync(ct);
        var stockIds = stockList.Select(s => s.Id).ToList();

        var balances = await db.StockMovements.AsNoTracking().Where(m => stockIds.Contains(m.StockId))
            .GroupBy(m => new { m.StockId, m.ProductId })
            .Select(g => new { g.Key.StockId, g.Key.ProductId, Balance = g.Sum(m => m.QuantityDelta) }).ToListAsync(ct);
        var products = await db.Products.AsNoTracking().Where(p => p.IsActive && p.MinimumStock > 0).ToListAsync(ct);

        var alerts = new List<StockAlertDto>();
        foreach (var stock in stockList)
            foreach (var p in products)
            {
                var balance = balances.FirstOrDefault(b => b.StockId == stock.Id && b.ProductId == p.Id)?.Balance ?? 0m;
                // Só alerta produtos que já tiveram movimentação naquele estoque (evita ruído).
                var hasHistory = balances.Any(b => b.StockId == stock.Id && b.ProductId == p.Id);
                if (hasHistory && StockRules.IsBelowMinimum(balance, p.MinimumStock))
                    alerts.Add(new StockAlertDto(stock.Id, stock.Name, p.Id, p.Name, p.Unit, balance, p.MinimumStock));
            }
        return alerts.OrderBy(a => a.StockName).ThenBy(a => a.ProductName).ToList();
    }

    public async Task<IReadOnlyList<MovementDto>> HistoryAsync(Guid? stockId, Guid? productId, Guid? sessionId,
        DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken ct)
    {
        var q = db.StockMovements.AsNoTracking().AsQueryable();
        if (!current.IsAdmin())
        {
            var allowed = await AllowedStockIdsAsync(ct);
            q = q.Where(m => allowed.Contains(m.StockId));
        }
        if (stockId.HasValue) q = q.Where(m => m.StockId == stockId);
        if (productId.HasValue) q = q.Where(m => m.ProductId == productId);
        if (sessionId.HasValue) q = q.Where(m => m.SessionId == sessionId);
        if (from.HasValue) q = q.Where(m => m.CreatedAt >= from);
        if (to.HasValue) q = q.Where(m => m.CreatedAt <= to);
        var rows = await q.OrderByDescending(m => m.CreatedAt).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    // ---------- Movimentações (casos de uso, nunca uma operação genérica) ----------

    public async Task<MovementDto> RegisterEntryAsync(EntryRequest r, CancellationToken ct)
    {
        if (r.UnitCost is null) throw new InvalidInputException("O valor pago por unidade é obrigatório na entrada.");
        if (r.InvoiceId.HasValue && !await db.Invoices.AnyAsync(i => i.Id == r.InvoiceId, ct))
            throw new InvalidInputException("Nota fiscal não encontrada.");

        return await AddAsync(r.StockId, r.ProductId, (uid, ip) =>
            StockMovement.Entry(r.ProductId, r.StockId, r.Quantity, r.UnitCost.Value, r.UnitSalePrice, r.InvoiceId, r.Notes, uid, ip), ct);
    }

    public async Task<MovementDto> RegisterUsageAsync(UsageRequest r, CancellationToken ct)
    {
        var session = await db.TattooSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == r.SessionId, ct)
                      ?? throw new NotFoundException("Sessão não encontrada.");
        if (!current.IsAdmin() && session.ArtistId != current.RequireArtistId())
            throw new ForbiddenException("Esta sessão pertence a outro tatuador.");
        if (!session.CanRegisterUsage)
            throw new ConflictException("Não é possível registrar uso em uma sessão concluída, cancelada ou com falta.");

        return await AddAsync(r.StockId, r.ProductId, (uid, ip) =>
            StockMovement.Usage(r.ProductId, r.StockId, r.SessionId, r.Quantity, r.UnitCost, r.UnitSalePrice, r.Notes, uid, ip), ct);
    }

    public Task<MovementDto> RegisterDamagedAsync(DamagedRequest r, CancellationToken ct) =>
        AddAsync(r.StockId, r.ProductId, (uid, ip) =>
            StockMovement.Damaged(r.ProductId, r.StockId, r.Quantity, r.Reason, r.Notes, uid, ip), ct);

    public Task<MovementDto> RegisterAdjustmentAsync(AdjustmentRequest r, CancellationToken ct) =>
        AddAsync(r.StockId, r.ProductId, (uid, ip) =>
            StockMovement.InventoryAdjustment(r.ProductId, r.StockId, r.QuantityToReduce, r.Reason, r.Notes, uid, ip), ct);

    /// <summary>RN09: correção por nova movimentação (admin). Nunca UPDATE/DELETE.</summary>
    public async Task<MovementDto> CorrectAsync(Guid movementId, CorrectionRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        var original = await db.StockMovements.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movementId, ct)
                       ?? throw new NotFoundException("Movimentação não encontrada.");
        if (await db.StockMovements.AnyAsync(m => m.ReversesMovementId == movementId, ct))
            throw new ConflictException("Esta movimentação já foi corrigida.");

        return await AddAsync(original.StockId, original.ProductId, (uid, ip) =>
            StockMovement.CreateCorrection(original, r.Reason, uid, ip), ct);
    }

    // ---------- Núcleo ----------

    private async Task<MovementDto> AddAsync(Guid stockId, Guid productId,
        Func<Guid, string?, StockMovement> factory, CancellationToken ct)
    {
        var userId = current.RequireUserId();   // RN08
        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Id == stockId, ct)
                    ?? throw new NotFoundException("Estoque não encontrado.");
        if (!stock.IsActive) throw new ConflictException("Estoque desativado.");
        await EnsureCanUseStockAsync(stockId, ct);   // RN07

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Produto não encontrado.");

        var movement = factory(userId, current.IpAddress);
        if (!product.IsActive && movement.QuantityDelta > 0)
            throw new ConflictException("Produto desativado.");

        if (movement.QuantityDelta < 0)   // RN01
        {
            var balance = await db.StockMovements.Where(m => m.StockId == stockId && m.ProductId == productId)
                .SumAsync(m => (decimal?)m.QuantityDelta, ct) ?? 0m;
            StockRules.EnsureSufficientBalance(balance, -movement.QuantityDelta);
        }

        db.StockMovements.Add(movement);
        stock.Touch();   // serializa movimentações concorrentes do mesmo estoque
        await db.SaveChangesAsync(ct);
        return ToDto(movement);
    }

    private async Task<List<Guid>> AllowedStockIdsAsync(CancellationToken ct)
    {
        var artistId = current.RequireArtistId();
        return await db.ArtistStockAccesses.Where(a => a.ArtistId == artistId).Select(a => a.StockId).ToListAsync(ct);
    }

    private async Task EnsureCanUseStockAsync(Guid stockId, CancellationToken ct)
    {
        if (current.IsAdmin()) return;
        var artistId = current.RequireArtistId();
        if (!await db.ArtistStockAccesses.AnyAsync(a => a.ArtistId == artistId && a.StockId == stockId, ct))
            throw new ForbiddenException("Você não tem acesso a este estoque.");
    }

    private Task EnsureCanViewStockAsync(Guid stockId, CancellationToken ct) => EnsureCanUseStockAsync(stockId, ct);

    private static StockDto ToDto(Stock s) => new(s.Id, s.Name, s.IsMain, s.OwnerArtistId, s.IsActive);

    private static MovementDto ToDto(StockMovement m) => new(m.Id, m.StockId, m.ProductId, m.Type, m.Quantity,
        m.QuantityDelta, m.UnitCost, m.UnitSalePrice, m.SessionId, m.InvoiceId, m.ReversesMovementId, m.Reason,
        m.Notes, m.UserId, m.CreatedAt);
}
