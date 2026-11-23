using GumpStudio.Application.Common;
using GumpStudio.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Api.Infrastructure;

/// <summary>Tratamento global de exceções (MVP 1) → ProblemDetails (RFC 9457).</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Não encontrado"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Não autenticado"),
            ForbiddenException or AccessDeniedException => (StatusCodes.Status403Forbidden, "Acesso negado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflito"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito de concorrência"),
            InvalidInputException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };

        if (status >= 500) logger.LogError(exception, "Erro não tratado em {Path}", context.Request.Path);
        else logger.LogInformation("{Type} em {Path}: {Message}", exception.GetType().Name, context.Request.Path, exception.Message);

        var detail = status switch
        {
            >= 500 => "Ocorreu um erro inesperado. Informe o código de correlação ao suporte.",
            StatusCodes.Status409Conflict when exception is DbUpdateConcurrencyException =>
                "Outra operação alterou este registro ao mesmo tempo. Tente novamente.",
            _ => exception.Message
        };

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = context.Request.Path };
        problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
        return true;
    }
}
