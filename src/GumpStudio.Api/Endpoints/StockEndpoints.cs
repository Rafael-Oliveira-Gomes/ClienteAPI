using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace GumpStudio.Api.Endpoints;

public static class ProductEndpoints
{
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { ["image/jpeg"] = "image/jpeg", ["image/png"] = "image/png", ["image/webp"] = "image/webp" };

    public static void MapProductEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/products").WithTags("Produtos");
        g.MapGet("/", (string? search, bool? activeOnly, ProductService s, CancellationToken ct) => s.SearchAsync(search, activeOnly ?? true, ct));
        g.MapGet("/{id:guid}", (Guid id, ProductService s, CancellationToken ct) => s.GetAsync(id, ct));
        g.MapPost("/", async (ProductRequest r, ProductService s, CancellationToken ct) =>
        {
            var dto = await s.CreateAsync(r, ct);
            return Results.Created($"/api/products/{dto.Id}", dto);
        });
        g.MapPut("/{id:guid}", (Guid id, ProductRequest r, ProductService s, CancellationToken ct) => s.UpdateAsync(id, r, ct));
        g.MapPost("/{id:guid}/activate", async (Guid id, ProductService s, CancellationToken ct) => { await s.SetActiveAsync(id, true, ct); return Results.NoContent(); });
        g.MapPost("/{id:guid}/deactivate", async (Guid id, ProductService s, CancellationToken ct) => { await s.SetActiveAsync(id, false, ct); return Results.NoContent(); });

        g.MapPost("/{id:guid}/image", async (Guid id, IFormFile file, ProductService s, IFileStorage storage, CancellationToken ct) =>
        {
            if (!ImageTypes.ContainsKey(file.ContentType)) throw new InvalidInputException("Envie uma imagem JPG, PNG ou WEBP.");
            await using var stream = file.OpenReadStream();
            var key = await storage.SaveAsync("products", file.FileName, stream, ct);
            await s.SetImageAsync(id, key, ct);
            return Results.NoContent();
        }).DisableAntiforgery();

        g.MapGet("/{id:guid}/image", async (Guid id, ProductService s, IFileStorage storage, CancellationToken ct) =>
        {
            var p = await s.GetAsync(id, ct);
            if (p.ImagePath is null) return Results.NotFound();
            var ext = Path.GetExtension(p.ImagePath).ToLowerInvariant();
            var type = ext switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
            return Results.File(await storage.OpenReadAsync(p.ImagePath, ct), type);
        });
    }
}

public static class StockEndpoints
{
    public static void MapStockEndpoints(this RouteGroupBuilder api)
    {
        var stocks = api.MapGroup("/stocks").WithTags("Estoques");
        stocks.MapGet("/", (StockService s, CancellationToken ct) => s.ListAsync(ct));
        stocks.MapGet("/alerts", (StockService s, CancellationToken ct) => s.LowStockAlertsAsync(ct));
        stocks.MapGet("/{id:guid}/balances", (Guid id, StockService s, CancellationToken ct) => s.BalancesAsync(id, ct));
        stocks.MapPost("/", async (StockRequest r, StockService s, CancellationToken ct) =>
        {
            var dto = await s.CreateAsync(r, ct);
            return Results.Created($"/api/stocks/{dto.Id}", dto);
        });
        stocks.MapPut("/{id:guid}", (Guid id, StockUpdateRequest r, StockService s, CancellationToken ct) => s.UpdateAsync(id, r, ct));

        // Casos de uso explícitos em vez de uma operação genérica (seção 18).
        var m = api.MapGroup("/stock-movements").WithTags("Movimentações de estoque");
        m.MapGet("/", (Guid? stockId, Guid? productId, Guid? sessionId, DateTimeOffset? from, DateTimeOffset? to, int? take,
                StockService s, CancellationToken ct) => s.HistoryAsync(stockId, productId, sessionId, from, to, take ?? 100, ct));
        m.MapPost("/entry", (EntryRequest r, StockService s, CancellationToken ct) => s.RegisterEntryAsync(r, ct));
        m.MapPost("/usage", (UsageRequest r, StockService s, CancellationToken ct) => s.RegisterUsageAsync(r, ct));
        m.MapPost("/damaged", (DamagedRequest r, StockService s, CancellationToken ct) => s.RegisterDamagedAsync(r, ct));
        m.MapPost("/inventory-adjustment", (AdjustmentRequest r, StockService s, CancellationToken ct) => s.RegisterAdjustmentAsync(r, ct));
        m.MapPost("/{id:guid}/correction", (Guid id, CorrectionRequest r, StockService s, CancellationToken ct) => s.CorrectAsync(id, r, ct));
    }
}
