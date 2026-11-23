using System.Diagnostics;

namespace GumpStudio.Api.Infrastructure;

/// <summary>Correlation ID + log de método, rota, status e tempo de resposta (seção 35).</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[Header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) id = Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = id;
        context.Response.Headers[Header] = id;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            var sw = Stopwatch.StartNew();
            try { await next(context); }
            finally
            {
                sw.Stop();
                logger.LogInformation("HTTP {Method} {Path} → {Status} em {ElapsedMs} ms",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}
