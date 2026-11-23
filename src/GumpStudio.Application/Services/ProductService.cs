using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Application.Services;

public sealed record ProductRequest(string Name, UnitOfMeasure Unit, string? Code, decimal PurchasePrice,
    decimal? SalePrice, decimal MinimumStock);

public sealed record ProductDto(Guid Id, string Name, UnitOfMeasure Unit, string? Code, string? ImagePath,
    decimal PurchasePrice, decimal? SalePrice, decimal MinimumStock, bool IsActive);

public sealed class ProductService(IAppDbContext db, ICurrentUser current)
{
    public async Task<IReadOnlyList<ProductDto>> SearchAsync(string? search, bool activeOnly, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(term) || (p.Code != null && p.Code.ToLower().Contains(term)));
        }
        return (await q.OrderBy(p => p.Name).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
              ?? throw new NotFoundException("Produto não encontrado."));

    public async Task<ProductDto> CreateAsync(ProductRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        await EnsureUniqueCode(r.Code, null, ct);
        var p = Product.Create(r.Name, r.Unit, r.Code, r.PurchasePrice, r.SalePrice, r.MinimumStock);
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Produto não encontrado.");
        await EnsureUniqueCode(r.Code, id, ct);
        p.Update(r.Name, r.Unit, r.Code, r.PurchasePrice, r.SalePrice, r.MinimumStock);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        current.EnsureAdmin();
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Produto não encontrado.");
        if (active) p.Activate(); else p.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    public async Task SetImageAsync(Guid id, string storageKey, CancellationToken ct)
    {
        current.EnsureAdmin();
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Produto não encontrado.");
        p.SetImage(storageKey);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueCode(string? code, Guid? ignoreId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var c = code.Trim();
        if (await db.Products.AnyAsync(p => p.Code == c && p.Id != ignoreId, ct))
            throw new ConflictException("Já existe um produto com este código.");
    }

    internal static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Unit, p.Code, p.ImagePath, p.PurchasePrice,
        p.SalePrice, p.MinimumStock, p.IsActive);
}
