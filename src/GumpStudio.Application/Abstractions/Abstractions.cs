using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GumpStudio.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<TattooArtist> TattooArtists { get; }
    DbSet<ArtistCompensation> ArtistCompensations { get; }
    DbSet<ArtistStockAccess> ArtistStockAccesses { get; }
    DbSet<Client> Clients { get; }
    DbSet<TattooSession> TattooSessions { get; }
    DbSet<ExamDocument> ExamDocuments { get; }
    DbSet<Product> Products { get; }
    DbSet<Stock> Stocks { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<FinancialTransaction> FinancialTransactions { get; }
    DbSet<StudioGoogleConnection> GoogleConnections { get; }
    EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    UserRole? Role { get; }
    Guid? ArtistId { get; }
    string? IpAddress { get; }
}

public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

public interface IGoogleIdTokenValidator
{
    /// <summary>Valida o ID token emitido pelo Login com Google. Lança UnauthorizedException se inválido.</summary>
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, Guid? artistId);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public sealed record CalendarEventData(string EventId, string Title, string Description,
    DateTimeOffset Start, DateTimeOffset End, string? Location);

public interface ICalendarGateway
{
    Task<bool> IsConnectedAsync(CancellationToken ct);
    /// <summary>Cria ou atualiza (idempotente pelo EventId). Retorna o id do evento no Google.</summary>
    Task<string> UpsertEventAsync(CalendarEventData data, CancellationToken ct);
    Task CancelEventAsync(string eventId, CancellationToken ct);
}

public sealed record CalendarStatusDto(bool Connected, string? GoogleAccountEmail, string? CalendarId, DateTimeOffset? ConnectedAt);

public interface ICalendarConnectionService
{
    Task<CalendarStatusDto> GetStatusAsync(CancellationToken ct);
    string BuildAuthorizationUrl(Guid adminUserId);
    /// <summary>Troca o code do OAuth pelo refresh token e salva a conexão.</summary>
    Task ConnectAsync(string code, string state, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
}

public interface IFileStorage
{
    Task<string> SaveAsync(string folder, string originalFileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

public static class CurrentUserExtensions
{
    public static bool IsAdmin(this ICurrentUser u) => u.Role == UserRole.Admin;

    public static Guid RequireUserId(this ICurrentUser u) =>
        u.UserId ?? throw new Common.UnauthorizedException("Usuário não autenticado.");

    public static Guid RequireArtistId(this ICurrentUser u) =>
        u.ArtistId ?? throw new Common.ForbiddenException("Usuário não possui perfil de tatuador.");

    public static void EnsureAdmin(this ICurrentUser u)
    {
        if (!u.IsAdmin()) throw new Common.ForbiddenException("Apenas administradores podem executar esta ação.");
    }
}
