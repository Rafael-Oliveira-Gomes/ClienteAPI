using GumpStudio.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GumpStudio.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ArtistService>();
        services.AddScoped<ClientService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<SessionService>();
        services.AddScoped<ExamService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
