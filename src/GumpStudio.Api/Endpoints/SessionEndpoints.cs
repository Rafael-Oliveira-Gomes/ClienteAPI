using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Application.Options;
using GumpStudio.Application.Services;
using GumpStudio.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GumpStudio.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/sessions").WithTags("Sessões");
        g.MapGet("/", (DateTimeOffset? from, DateTimeOffset? to, Guid? artistId, Guid? clientId, SessionStatus? status,
                SessionService s, CancellationToken ct) => s.ListAsync(from, to, artistId, clientId, status, ct));
        g.MapGet("/{id:guid}", (Guid id, SessionService s, CancellationToken ct) => s.GetAsync(id, ct));
        g.MapPost("/", async (CreateSessionRequest r, SessionService s, CancellationToken ct) =>
        {
            var dto = await s.CreateAsync(r, ct);
            return Results.Created($"/api/sessions/{dto.Id}", dto);
        });
        g.MapPut("/{id:guid}", (Guid id, UpdateSessionRequest r, SessionService s, CancellationToken ct) => s.UpdateAsync(id, r, ct));
        g.MapPost("/{id:guid}/confirm", (Guid id, SessionService s, CancellationToken ct) => s.ConfirmAsync(id, ct));
        g.MapPost("/{id:guid}/start", (Guid id, SessionService s, CancellationToken ct) => s.StartAsync(id, ct));
        g.MapPost("/{id:guid}/no-show", (Guid id, SessionService s, CancellationToken ct) => s.MarkNoShowAsync(id, ct));
        g.MapPost("/{id:guid}/cancel", (Guid id, CancelSessionRequest r, SessionService s, CancellationToken ct) => s.CancelAsync(id, r, ct));
        g.MapPost("/{id:guid}/complete", (Guid id, CompleteSessionRequest? r, SessionService s, CancellationToken ct) =>
            s.CompleteAsync(id, r ?? new CompleteSessionRequest(), ct));
    }
}

public static class ExamEndpoints
{
    public static void MapExamEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/exams").WithTags("Exames e documentos");

        g.MapPost("/", async ([FromForm] Guid clientId, [FromForm] Guid? sessionId, [FromForm] string type,
            [FromForm] DateOnly examDate, [FromForm] string? notes, IFormFile file, ExamService s, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var dto = await s.UploadAsync(clientId, sessionId, type, examDate, notes, file.FileName, file.ContentType, stream, ct);
            return Results.Created($"/api/exams/{dto.Id}", dto);
        }).DisableAntiforgery();

        g.MapGet("/", (Guid? clientId, Guid? sessionId, ExamService s, CancellationToken ct) => s.ListAsync(clientId, sessionId, ct));

        g.MapGet("/{id:guid}/file", async (Guid id, ExamService s, CancellationToken ct) =>
        {
            var f = await s.DownloadAsync(id, ct);
            return Results.File(f.Content, f.ContentType, f.FileName);
        });

        g.MapPut("/{id:guid}/status", (Guid id, SetDocumentStatusRequest r, ExamService s, CancellationToken ct) => s.SetStatusAsync(id, r.Status, ct));
    }
}

public sealed record SetDocumentStatusRequest(DocumentStatus Status);

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/invoices").WithTags("Notas fiscais").RequireAuthorization("Admin");
        g.MapGet("/", (InvoiceService s, CancellationToken ct) => s.ListAsync(ct));
        g.MapGet("/{id:guid}", (Guid id, InvoiceService s, CancellationToken ct) => s.GetAsync(id, ct));

        g.MapPost("/", async ([FromForm] string number, [FromForm] string? series, [FromForm] DateOnly issueDate,
            [FromForm] string supplier, [FromForm] decimal totalValue, IFormFile? file, InvoiceService s, CancellationToken ct) =>
        {
            var request = new InvoiceRequest(number, series, issueDate, supplier, totalValue);
            if (file is null)
                return Results.Created("/api/invoices", await s.CreateAsync(request, null, null, null, ct));

            await using var stream = file.OpenReadStream();
            return Results.Created("/api/invoices", await s.CreateAsync(request, file.FileName, file.ContentType, stream, ct));
        }).DisableAntiforgery();

        g.MapGet("/{id:guid}/file", async (Guid id, InvoiceService s, CancellationToken ct) =>
        {
            var f = await s.DownloadAsync(id, ct);
            return Results.File(f.Content, f.ContentType, f.FileName);
        });
    }
}

public sealed record ReverseTransactionRequest(string? Notes);

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/financial-transactions").WithTags("Financeiro").RequireAuthorization("Admin");
        g.MapGet("/", (DateOnly? from, DateOnly? to, TransactionType? type, string? category, Guid? artistId, Guid? sessionId,
                FinanceService s, CancellationToken ct) => s.ListAsync(from, to, type, category, artistId, sessionId, ct));
        g.MapGet("/summary", (DateOnly from, DateOnly to, FinanceService s, CancellationToken ct) => s.SummaryAsync(from, to, ct));
        g.MapPost("/", async (TransactionRequest r, FinanceService s, CancellationToken ct) =>
        {
            var dto = await s.CreateAsync(r, ct);
            return Results.Created($"/api/financial-transactions/{dto.Id}", dto);
        });
        g.MapPost("/{id:guid}/reversal", (Guid id, ReverseTransactionRequest r, FinanceService s, CancellationToken ct) => s.ReverseAsync(id, r.Notes, ct));
    }
}

public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/calendar").WithTags("Google Agenda");

        g.MapGet("/status", (ICalendarConnectionService c, CancellationToken ct) => c.GetStatusAsync(ct)).RequireAuthorization("Admin");

        // 1) Admin pede a URL de consentimento e abre no navegador.
        g.MapGet("/connect", (ICalendarConnectionService c, ICurrentUser u) =>
        {
            u.EnsureAdmin();
            return Results.Ok(new { url = c.BuildAuthorizationUrl(u.RequireUserId()) });
        }).RequireAuthorization("Admin");

        // 2) O Google redireciona para cá (navegador, sem Bearer). O "state" assinado identifica o admin.
        g.MapGet("/callback", async (string? code, string? state, string? error, ICalendarConnectionService c,
            IOptions<GoogleOptions> options, CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(error)) throw new InvalidInputException($"O Google recusou a conexão: {error}");
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state)) throw new InvalidInputException("Parâmetros do OAuth ausentes.");

            await c.ConnectAsync(code, state, ct);
            var back = options.Value.CalendarPostConnectRedirect;
            return string.IsNullOrWhiteSpace(back)
                ? Results.Ok(new { connected = true })
                : Results.Redirect($"{back}{(back.Contains('?') ? '&' : '?')}calendar=connected");
        }).AllowAnonymous();

        g.MapDelete("/connection", async (ICalendarConnectionService c, CancellationToken ct) =>
        {
            await c.DisconnectAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization("Admin");

        // Reenvio manual após falha da integração (a sessão nunca fica bloqueada pelo Google).
        g.MapPost("/sessions/{id:guid}/sync", (Guid id, SessionService s, CancellationToken ct) => s.ResyncCalendarAsync(id, ct));
    }
}
