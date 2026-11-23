using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Application.Services;

public sealed record ClientRequest(string Name, string? Cpf, string? Phone, string? WhatsApp, string? Email,
    DateOnly? BirthDate, string? Address, string? Notes);

public sealed record ClientDto(Guid Id, string Name, string? Cpf, string? Phone, string? WhatsApp, string? Email,
    DateOnly? BirthDate, string? Address, string? Notes, DateTimeOffset CreatedAt);

public sealed record ClientHistoryItem(Guid SessionId, DateTimeOffset StartsAt, string ArtistName, decimal Value,
    SessionStatus Status, int DocumentCount);

public sealed class ClientService(IAppDbContext db, ICurrentUser current)
{
    public async Task<PagedResult<ClientDto>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Visible();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term)
                || (c.Phone != null && c.Phone.Contains(term))
                || (c.WhatsApp != null && c.WhatsApp.Contains(term))
                || (c.Email != null && c.Email.Contains(term))
                || (c.Cpf != null && c.Cpf.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<ClientDto>(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<ClientDto> GetAsync(Guid id, CancellationToken ct)
    {
        var c = await Visible().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Cliente não encontrado.");
        return ToDto(c);
    }

    public async Task<ClientDto> CreateAsync(ClientRequest r, CancellationToken ct)
    {
        var client = Client.Create(r.Name, r.Cpf, r.Phone, r.WhatsApp, r.Email, r.BirthDate, r.Address, r.Notes);
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    public async Task<ClientDto> UpdateAsync(Guid id, ClientRequest r, CancellationToken ct)
    {
        var client = await Visible(tracking: true).FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw new NotFoundException("Cliente não encontrado.");
        client.Update(r.Name, r.Cpf, r.Phone, r.WhatsApp, r.Email, r.BirthDate, r.Address, r.Notes);
        await db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    public async Task<IReadOnlyList<ClientHistoryItem>> HistoryAsync(Guid id, CancellationToken ct)
    {
        await GetAsync(id, ct);   // aplica a regra de visibilidade
        var sessions = db.TattooSessions.AsNoTracking().Where(s => s.ClientId == id);
        if (!current.IsAdmin()) { var artistId = current.RequireArtistId(); sessions = sessions.Where(s => s.ArtistId == artistId); }

        var rows = await (from s in sessions
                          join a in db.TattooArtists on s.ArtistId equals a.Id
                          orderby s.StartsAt descending
                          select new { s.Id, s.StartsAt, ArtistName = a.Name, s.Value, s.Status }).ToListAsync(ct);
        var docCounts = await db.ExamDocuments.Where(d => d.ClientId == id && d.SessionId != null)
            .GroupBy(d => d.SessionId!.Value).Select(g => new { SessionId = g.Key, Count = g.Count() }).ToListAsync(ct);
        return rows.Select(r => new ClientHistoryItem(r.Id, r.StartsAt, r.ArtistName, r.Value, r.Status,
            docCounts.FirstOrDefault(d => d.SessionId == r.Id)?.Count ?? 0)).ToList();
    }

    /// <summary>Admin vê todos; tatuador vê clientes que cadastrou ou que atendeu (seção 3.2).</summary>
    private IQueryable<Client> Visible(bool tracking = false)
    {
        IQueryable<Client> q = tracking ? db.Clients : db.Clients.AsNoTracking();
        if (current.IsAdmin()) return q;
        var userId = current.RequireUserId();
        var artistId = current.RequireArtistId();
        return q.Where(c => c.CreatedBy == userId || db.TattooSessions.Any(s => s.ClientId == c.Id && s.ArtistId == artistId));
    }

    private static ClientDto ToDto(Client c) => new(c.Id, c.Name, c.Cpf, c.Phone, c.WhatsApp, c.Email, c.BirthDate,
        c.Address, c.Notes, c.CreatedAt);
}
