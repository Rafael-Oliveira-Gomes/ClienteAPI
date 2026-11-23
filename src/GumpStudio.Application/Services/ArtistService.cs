using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Application.Services;

public sealed record CompensationInput(CompensationType Type, decimal? MonthlyAmount, decimal? DailyAmount,
    decimal? ArtistPercentage, DateOnly? ValidFrom);

public sealed record CompensationDto(Guid Id, CompensationType Type, decimal? MonthlyAmount, decimal? DailyAmount,
    decimal? ArtistPercentage, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record ArtistDto(Guid Id, Guid UserId, string Name, string Email, string? Cpf, string? Phone,
    DateOnly? BirthDate, ArtistType Type, DateOnly StartDate, DateOnly? EndDate, bool IsActive,
    CompensationDto? CurrentCompensation, IReadOnlyList<Guid> StockIds);

public sealed record CreateArtistRequest(string Name, string Email, string? Cpf, string? Phone, DateOnly? BirthDate,
    ArtistType Type, DateOnly StartDate, DateOnly? EndDate, CompensationInput Compensation, IReadOnlyList<Guid>? StockIds);

public sealed record UpdateArtistRequest(string Name, string? Cpf, string? Phone, DateOnly? BirthDate,
    ArtistType Type, DateOnly StartDate, DateOnly? EndDate);

public sealed class ArtistService(IAppDbContext db, ICurrentUser current, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<IReadOnlyList<ArtistDto>> ListAsync(CancellationToken ct)
    {
        current.EnsureAdmin();
        var artists = await db.TattooArtists.AsNoTracking()
            .Include(a => a.Compensations).Include(a => a.StockAccesses)
            .OrderBy(a => a.Name).ToListAsync(ct);
        return artists.Select(ToDto).ToList();
    }

    public async Task<ArtistDto> GetAsync(Guid id, CancellationToken ct)
    {
        if (!current.IsAdmin() && current.ArtistId != id) throw new ForbiddenException("Acesso negado.");
        var artist = await db.TattooArtists.AsNoTracking()
            .Include(a => a.Compensations).Include(a => a.StockAccesses)
            .FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Tatuador não encontrado.");
        return ToDto(artist);
    }

    public async Task<ArtistDto> CreateAsync(CreateArtistRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        var email = r.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw new ConflictException("Já existe um usuário com este e-mail.");

        // Rotativo: acesso provisório com início e fim (seção 3.3).
        DateTimeOffset? from = null, until = null;
        if (r.Type == ArtistType.Rotating)
        {
            from = new DateTimeOffset(r.StartDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            until = r.EndDate is { } end ? new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
        }

        var user = User.Create(email, r.Name, UserRole.Artist, from, until);
        var compensation = BuildCompensation(r.Compensation);
        var artist = TattooArtist.Create(user.Id, r.Name, email, r.Type, r.StartDate, r.EndDate, compensation,
            r.Cpf, r.Phone, r.BirthDate);
        if (r.StockIds is { Count: > 0 }) artist.SetStockAccess(await ValidStocks(r.StockIds, ct));

        db.Users.Add(user);
        db.TattooArtists.Add(artist);
        await db.SaveChangesAsync(ct);
        return ToDto(artist);
    }

    public async Task<ArtistDto> UpdateAsync(Guid id, UpdateArtistRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        var artist = await Load(id, ct);
        artist.UpdateProfile(r.Name, r.Cpf, r.Phone, r.BirthDate);
        artist.SetPeriod(r.Type, r.StartDate, r.EndDate);

        var user = await db.Users.FirstAsync(u => u.Id == artist.UserId, ct);
        user.Rename(r.Name);
        if (r.Type == ArtistType.Rotating)
            user.SetAccessWindow(new DateTimeOffset(r.StartDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                new DateTimeOffset(r.EndDate!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        else
            user.SetAccessWindow(null, null);

        await db.SaveChangesAsync(ct);
        return ToDto(artist);
    }

    public async Task<ArtistDto> ChangeCompensationAsync(Guid id, CompensationInput input, CancellationToken ct)
    {
        current.EnsureAdmin();
        var artist = await Load(id, ct);
        artist.ChangeCompensation(BuildCompensation(input));
        await db.SaveChangesAsync(ct);
        return ToDto(artist);
    }

    public async Task<ArtistDto> SetStocksAsync(Guid id, IReadOnlyList<Guid> stockIds, CancellationToken ct)
    {
        current.EnsureAdmin();
        var artist = await Load(id, ct);
        artist.SetStockAccess(await ValidStocks(stockIds, ct));
        await db.SaveChangesAsync(ct);
        return ToDto(artist);
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        current.EnsureAdmin();
        var artist = await Load(id, ct);
        var user = await db.Users.FirstAsync(u => u.Id == artist.UserId, ct);
        if (active) { artist.Activate(); user.Activate(); }
        else
        {
            artist.Deactivate(); user.Deactivate();
            var now = clock.GetUtcNow();
            foreach (var t in await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct))
                t.Revoke(now);
        }
        await db.SaveChangesAsync(ct);
    }

    private ArtistCompensation BuildCompensation(CompensationInput i) =>
        ArtistCompensation.Create(i.Type, i.MonthlyAmount, i.DailyAmount, i.ArtistPercentage, i.ValidFrom ?? Today);

    private async Task<List<Guid>> ValidStocks(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        var found = await db.Stocks.Where(s => wanted.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        if (found.Count != wanted.Count) throw new InvalidInputException("Um ou mais estoques informados não existem.");
        return wanted;
    }

    private async Task<TattooArtist> Load(Guid id, CancellationToken ct) =>
        await db.TattooArtists.Include(a => a.Compensations).Include(a => a.StockAccesses)
            .FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Tatuador não encontrado.");

    private ArtistDto ToDto(TattooArtist a)
    {
        var c = a.CompensationOn(Today);
        return new ArtistDto(a.Id, a.UserId, a.Name, a.Email, a.Cpf, a.Phone, a.BirthDate, a.Type, a.StartDate,
            a.EndDate, a.IsActive,
            c is null ? null : new CompensationDto(c.Id, c.Type, c.MonthlyAmount, c.DailyAmount, c.ArtistPercentage, c.ValidFrom, c.ValidTo),
            a.StockAccesses.Select(s => s.StockId).ToList());
    }
}
