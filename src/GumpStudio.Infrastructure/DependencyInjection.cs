using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Options;
using GumpStudio.Infrastructure.GoogleCalendar;
using GumpStudio.Infrastructure.Identity;
using GumpStudio.Infrastructure.Persistence;
using GumpStudio.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GumpStudio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.Configure<GoogleOptions>(config.GetSection(GoogleOptions.Section));
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));

        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString("Default"), n => n.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddScoped<GoogleCalendarGateway>();
        services.AddScoped<ICalendarGateway>(sp => sp.GetRequiredService<GoogleCalendarGateway>());
        services.AddScoped<ICalendarConnectionService>(sp => sp.GetRequiredService<GoogleCalendarGateway>());
        return services;
    }
}
