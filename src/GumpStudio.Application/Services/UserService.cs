using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Application.Services;

public sealed record CreateUserRequest(string Email, string Name, UserRole Role,
    DateTimeOffset? AccessStartsAt, DateTimeOffset? AccessExpiresAt);
public sealed record SetAccessWindowRequest(DateTimeOffset? AccessStartsAt, DateTimeOffset? AccessExpiresAt);

/// <summary>Gestão de usuários (somente administrador).</summary>
public sealed class UserService(IAppDbContext db, ICurrentUser current, TimeProvider clock)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        current.EnsureAdmin();
        var now = clock.GetUtcNow();
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct);
        var artists = await db.TattooArtists.AsNoTracking().Select(a => new { a.UserId, a.Id }).ToListAsync(ct);
        return users.Select(u => UserMapping.ToDto(u, artists.FirstOrDefault(a => a.UserId == u.Id)?.Id, now)).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        var email = r.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw new ConflictException("Já existe um usuário com este e-mail.");
        var user = User.Create(email, r.Name, r.Role, r.AccessStartsAt, r.AccessExpiresAt);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return UserMapping.ToDto(user, null, clock.GetUtcNow());
    }

    public async Task<UserDto> SetAccessWindowAsync(Guid id, SetAccessWindowRequest r, CancellationToken ct)
    {
        current.EnsureAdmin();
        var user = await Load(id, ct);
        user.SetAccessWindow(r.AccessStartsAt, r.AccessExpiresAt);
        await db.SaveChangesAsync(ct);
        return UserMapping.ToDto(user, null, clock.GetUtcNow());
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        current.EnsureAdmin();
        if (!active && id == current.UserId) throw new InvalidInputException("Você não pode desativar o próprio usuário.");
        var user = await Load(id, ct);
        if (active) user.Activate(); else user.Deactivate();

        if (!active)   // revoga sessões abertas
        {
            var now = clock.GetUtcNow();
            var tokens = await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(ct);
            foreach (var t in tokens) t.Revoke(now);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<User> Load(Guid id, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("Usuário não encontrado.");
}
