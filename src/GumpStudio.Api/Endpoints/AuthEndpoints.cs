using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Services;

namespace GumpStudio.Api.Endpoints;

public sealed record GoogleLoginRequest(string IdToken);
public sealed record RefreshRequest(string RefreshToken);
public sealed record DevLoginRequest(string Email);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/auth").WithTags("Auth");

        // O front obtém o ID token com "Entrar com Google" (Google Identity Services) e envia aqui.
        g.MapPost("/google", (GoogleLoginRequest r, AuthService s, CancellationToken ct) => s.LoginWithGoogleAsync(r.IdToken, ct))
            .AllowAnonymous().RequireRateLimiting("auth");

        g.MapPost("/refresh", (RefreshRequest r, AuthService s, CancellationToken ct) => s.RefreshAsync(r.RefreshToken, ct))
            .AllowAnonymous().RequireRateLimiting("auth");

        g.MapPost("/logout", async (RefreshRequest r, AuthService s, CancellationToken ct) =>
        {
            await s.LogoutAsync(r.RefreshToken, ct);
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapGet("/me", (ICurrentUser u, AuthService s, CancellationToken ct) => s.GetProfileAsync(u.RequireUserId(), ct));
    }
}

public static class DevAuthEndpoints
{
    /// <summary>Só mapeado em Development (ver Program.cs). Nunca existe em produção.</summary>
    public static void MapDevAuthEndpoints(this RouteGroupBuilder api) =>
        api.MapPost("/auth/dev-login", (DevLoginRequest r, AuthService s, CancellationToken ct) => s.DevLoginAsync(r.Email, ct))
            .AllowAnonymous().WithTags("Auth (somente dev)");
}

public static class UserEndpoints
{
    public static void MapUserEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/users").WithTags("Usuários").RequireAuthorization("Admin");
        g.MapGet("/", (UserService s, CancellationToken ct) => s.ListAsync(ct));
        g.MapPost("/", async (CreateUserRequest r, UserService s, CancellationToken ct) =>
            Results.Created("/api/users", await s.CreateAsync(r, ct)));
        g.MapPut("/{id:guid}/access", (Guid id, SetAccessWindowRequest r, UserService s, CancellationToken ct) => s.SetAccessWindowAsync(id, r, ct));
        g.MapPost("/{id:guid}/activate", async (Guid id, UserService s, CancellationToken ct) => { await s.SetActiveAsync(id, true, ct); return Results.NoContent(); });
        g.MapPost("/{id:guid}/deactivate", async (Guid id, UserService s, CancellationToken ct) => { await s.SetActiveAsync(id, false, ct); return Results.NoContent(); });
    }
}

public sealed record SetStocksRequest(IReadOnlyList<Guid> StockIds);

public static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/artists").WithTags("Tatuadores");
        g.MapGet("/", (ArtistService s, CancellationToken ct) => s.ListAsync(ct));
        g.MapGet("/{id:guid}", (Guid id, ArtistService s, CancellationToken ct) => s.GetAsync(id, ct));
        g.MapPost("/", async (CreateArtistRequest r, ArtistService s, CancellationToken ct) =>
        {
            var dto = await s.CreateAsync(r, ct);
            return Results.Created($"/api/artists/{dto.Id}", dto);
        });
        g.MapPut("/{id:guid}", (Guid id, UpdateArtistRequest r, ArtistService s, CancellationToken ct) => s.UpdateAsync(id, r, ct));
        g.MapPut("/{id:guid}/compensation", (Guid id, CompensationInput r, ArtistService s, CancellationToken ct) => s.ChangeCompensationAsync(id, r, ct));
        g.MapPut("/{id:guid}/stocks", (Guid id, SetStocksRequest r, ArtistService s, CancellationToken ct) => s.SetStocksAsync(id, r.StockIds, ct));
        g.MapPost("/{id:guid}/activate", async (Guid id, ArtistService s, CancellationToken ct) => { await s.SetActiveAsync(id, true, ct); return Results.NoContent(); });
        g.MapPost("/{id:guid}/deactivate", async (Guid id, ArtistService s, CancellationToken ct) => { await s.SetActiveAsync(id, false, ct); return Results.NoContent(); });
    }
}

public static class ClientEndpoints
{
    public static void MapClientEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/clients").WithTags("Clientes");
        g.MapGet("/", (string? search, int? page, int? pageSize, ClientService s, CancellationToken ct) =>
            s.SearchAsync(search, page ?? 1, pageSize ?? 20, ct));
        g.MapGet("/{id:guid}", (Guid id, ClientService s, CancellationToken ct) => s.GetAsync(id, ct));
        g.MapGet("/{id:guid}/history", (Guid id, ClientService s, CancellationToken ct) => s.HistoryAsync(id, ct));
        g.MapPost("/", async (ClientRequest r, ClientService s, CancellationToken ct) =>
        {
            var dto = await s.CreateAsync(r, ct);
            return Results.Created($"/api/clients/{dto.Id}", dto);
        });
        g.MapPut("/{id:guid}", (Guid id, ClientRequest r, ClientService s, CancellationToken ct) => s.UpdateAsync(id, r, ct));
    }
}

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this RouteGroupBuilder api) =>
        api.MapGet("/dashboard", (DashboardService s, CancellationToken ct) => s.GetAsync(ct)).WithTags("Dashboard");
}
