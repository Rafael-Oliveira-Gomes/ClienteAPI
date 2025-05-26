using Client.API.Extensions.SwaggerConfigurations;
using Client.PostgreSQL.Repositories;
using Cliente.Application.Handlers;


public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configuração de serviços
        builder.Services
            .AddSwaggerConfig(builder.Configuration)
            .AddControllers();

        // Adiciona a configuração do MongoDB e repositórios
        builder.Services.AddRepository(builder.Configuration);
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IncluirClienteHandler).Assembly));

        var app = builder.Build();

        app.UsePathBase("/cliente-api");


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
