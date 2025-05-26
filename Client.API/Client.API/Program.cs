using Client.API.Extensions.SwaggerConfigurations;
using Client.PostgreSQL.Repositories;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configura��o de servi�os
        builder.Services
            .AddSwaggerConfig(builder.Configuration)
            //.AddApplication(builder.Configuration)
            .AddControllers();

        // Adiciona a configura��o do MongoDB e reposit�rios
        builder.Services.AddRepository(builder.Configuration);
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
