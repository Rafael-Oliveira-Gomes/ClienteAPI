using Client.Domain.Entities;
using Client.Domain.Repositories;
using Client.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Client.PostgreSQL.Repositories;

public static class AddRepositorySetup
{
    public static IServiceCollection AddRepository(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ClientContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("PostgresConnection"));
        });
        services.AddScoped<IBaseRepository<Cliente>, BaseRepository<Cliente>>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<ITatuadorRepository, TatuadorRepository>();
        return services;
    }
}
