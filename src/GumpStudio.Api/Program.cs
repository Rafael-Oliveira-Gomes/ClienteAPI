using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GumpStudio.Api.Endpoints;
using GumpStudio.Api.Infrastructure;
using GumpStudio.Application;
using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Options;
using GumpStudio.Domain.Enums;
using GumpStudio.Infrastructure;
using GumpStudio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configuração local da máquina (connection string, segredos). Não versionado.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ---------- Serviços ----------
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecurityTransformer>());

// A chave de Data Protection protege o refresh token do Google: precisa persistir entre deploys.
builder.Services.AddDataProtection()
    .SetApplicationName("GumpStudio")
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "keys"));

// Autenticação: o login é feito com Google; a API emite o próprio JWT.
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey deve ter pelo menos 32 caracteres (defina por variável de ambiente/secret).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "sub", RoleClaimType = "role"
    };
    o.Events = new JwtBearerEvents
    {
        // Não basta o token ser válido: o usuário precisa continuar ativo e, se rotativo, dentro da janela de acesso (seção 19).
        OnTokenValidated = async ctx =>
        {
            if (!Guid.TryParse(ctx.Principal?.FindFirst("sub")?.Value, out var userId)) { ctx.Fail("Token inválido."); return; }
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var clock = ctx.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ctx.HttpContext.RequestAborted);
            if (user is null || user.GetAccessStatus(clock.GetUtcNow()) != AccessStatus.Allowed)
                ctx.Fail("Acesso bloqueado ou expirado.");
        }
    };
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole(UserRole.Admin.ToString()))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddCors(o => o.AddPolicy("front", p =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders(CorrelationIdMiddleware.Header);
}));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgresql");

var app = builder.Build();

// ---------- Migrations ----------
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// ---------- Pipeline ----------
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseCors("front");

// O Swagger UI é middleware (sem endpoint): precisa vir ANTES da autorização, senão a política
// padrão ("exigir login") responde 401 para a página.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "GumpStudio API v1");
        o.RoutePrefix = "swagger";
    });
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();

var api = app.MapGroup("/api");
api.MapAuthEndpoints();
if (app.Environment.IsDevelopment()) api.MapDevAuthEndpoints();
api.MapUserEndpoints();
api.MapArtistEndpoints();
api.MapClientEndpoints();
api.MapProductEndpoints();
api.MapStockEndpoints();
api.MapSessionEndpoints();
api.MapExamEndpoints();
api.MapInvoiceEndpoints();
api.MapFinanceEndpoints();
api.MapCalendarEndpoints();
api.MapDashboardEndpoints();

app.Run();

public partial class Program;
