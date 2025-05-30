using Client.API.Extensions.SwaggerConfigurations;
using Client.API.Extensions;
using Client.PostgreSQL.Repositories;

using Cliente.Application.Handlers;

/// <summary>
/// Classe principal do aplicativo Cliente API.
/// </summary>
public class Program
{
    /// <summary>
    /// Ponto de entrada principal do aplicativo.
    /// </summary>
    /// <param name="args">Argumentos de linha de comando.</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configuração de serviços
        builder.Services
            .AddSwaggerConfig(builder.Configuration)
            .AddControllers();

        builder.Services.AddCustomCors(); // Mova a chamada para AddCustomCors aqui

        builder.Services.AddRepository(builder.Configuration);
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IncluirClienteHandler).Assembly));

        var app = builder.Build();

        app.UsePathBase("/cliente-api");

        app.UseCustomCors();

        app.UseRouting();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/cliente-api/swagger/v1/swagger.json", "Cliente API V1");
            options.RoutePrefix = string.Empty;
        });

        app.MapControllers();

        await app.RunAsync();
    }
}
