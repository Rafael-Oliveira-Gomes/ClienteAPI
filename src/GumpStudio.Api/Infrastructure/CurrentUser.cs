using System.Security.Claims;
using GumpStudio.Application.Abstractions;
using GumpStudio.Domain.Enums;

namespace GumpStudio.Api.Infrastructure;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;
    public Guid? ArtistId => Guid.TryParse(Principal?.FindFirst("artist_id")?.Value, out var id) ? id : null;
    public UserRole? Role => Enum.TryParse<UserRole>(Principal?.FindFirst("role")?.Value, out var r) ? r : null;
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
