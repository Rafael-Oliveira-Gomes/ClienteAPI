using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Application.Services;

public sealed record ExamDto(Guid Id, Guid ClientId, Guid? SessionId, string Type, DateOnly ExamDate, string? Notes,
    DocumentStatus Status, string FileName, string ContentType, DateTimeOffset CreatedAt);

public sealed record FileResult(Stream Content, string ContentType, string FileName);

public sealed record InvoiceRequest(string Number, string? Series, DateOnly IssueDate, string Supplier, decimal TotalValue);
public sealed record InvoiceDto(Guid Id, string Number, string? Series, DateOnly IssueDate, string Supplier,
    decimal TotalValue, bool HasFile, string? FileName);

/// <summary>Exames/documentos (seção 7). Arquivos nunca são servidos por URL pública: sempre via autorização.</summary>
public sealed class ExamService(IAppDbContext db, ICurrentUser current, IFileStorage storage)
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "application/pdf", "image/jpeg", "image/png", "image/webp" };

    public async Task<ExamDto> UploadAsync(Guid clientId, Guid? sessionId, string type, DateOnly examDate, string? notes,
        string fileName, string contentType, Stream content, CancellationToken ct)
    {
        if (!AllowedTypes.Contains(contentType)) throw new InvalidInputException("Formato não permitido. Use PDF, JPG, PNG ou WEBP.");
        await EnsureCanAccessClient(clientId, ct);
        if (sessionId.HasValue && !await db.TattooSessions.AnyAsync(s => s.Id == sessionId && s.ClientId == clientId, ct))
            throw new InvalidInputException("A sessão informada não pertence ao cliente.");

        var key = await storage.SaveAsync("exams", fileName, content, ct);
        var doc = ExamDocument.Create(clientId, sessionId, type, examDate, notes, key, Path.GetFileName(fileName), contentType);
        db.ExamDocuments.Add(doc);
        try { await db.SaveChangesAsync(ct); }
        catch { await storage.DeleteAsync(key, CancellationToken.None); throw; }
        return ToDto(doc);
    }

    public async Task<IReadOnlyList<ExamDto>> ListAsync(Guid? clientId, Guid? sessionId, CancellationToken ct)
    {
        if (clientId is null && sessionId is null) throw new InvalidInputException("Informe clientId ou sessionId.");
        var q = db.ExamDocuments.AsNoTracking().AsQueryable();
        if (clientId.HasValue) { await EnsureCanAccessClient(clientId.Value, ct); q = q.Where(d => d.ClientId == clientId); }
        if (sessionId.HasValue)
        {
            var cid = await db.TattooSessions.Where(s => s.Id == sessionId).Select(s => (Guid?)s.ClientId).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Sessão não encontrada.");
            await EnsureCanAccessClient(cid, ct);
            q = q.Where(d => d.SessionId == sessionId);
        }
        return (await q.OrderByDescending(d => d.ExamDate).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<FileResult> DownloadAsync(Guid id, CancellationToken ct)
    {
        var doc = await db.ExamDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct)
                  ?? throw new NotFoundException("Documento não encontrado.");
        await EnsureCanAccessClient(doc.ClientId, ct);
        return new FileResult(await storage.OpenReadAsync(doc.StorageKey, ct), doc.ContentType, doc.FileName);
    }

    public async Task<ExamDto> SetStatusAsync(Guid id, DocumentStatus status, CancellationToken ct)
    {
        current.EnsureAdmin();
        var doc = await db.ExamDocuments.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Documento não encontrado.");
        doc.SetStatus(status);
        await db.SaveChangesAsync(ct);
        return ToDto(doc);
    }

    /// <summary>Admin acessa tudo; tatuador, apenas clientes que cadastrou ou atende.</summary>
    private async Task EnsureCanAccessClient(Guid clientId, CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct)) throw new NotFoundException("Cliente não encontrado.");
        if (current.IsAdmin()) return;
        var userId = current.RequireUserId();
        var artistId = current.RequireArtistId();
        var allowed = await db.Clients.AnyAsync(c => c.Id == clientId && c.CreatedBy == userId, ct)
                      || await db.TattooSessions.AnyAsync(s => s.ClientId == clientId && s.ArtistId == artistId, ct);
        if (!allowed) throw new ForbiddenException("Você não tem acesso aos documentos deste cliente.");
    }

    private static ExamDto ToDto(ExamDocument d) => new(d.Id, d.ClientId, d.SessionId, d.Type, d.ExamDate, d.Notes,
        d.Status, d.FileName, d.ContentType, d.CreatedAt);
}

/// <summary>Notas fiscais de entrada de estoque (seção 14). Sem emissão fiscal no MVP.</summary>
public sealed class InvoiceService(IAppDbContext db, ICurrentUser current, IFileStorage storage)
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "application/pdf", "application/xml", "text/xml" };

    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct)
    {
        current.EnsureAdmin();
        return (await db.Invoices.AsNoTracking().OrderByDescending(i => i.IssueDate).Take(300).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<InvoiceDto> GetAsync(Guid id, CancellationToken ct)
    {
        current.EnsureAdmin();
        return ToDto(await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Nota fiscal não encontrada."));
    }

    public async Task<InvoiceDto> CreateAsync(InvoiceRequest r, string? fileName, string? contentType, Stream? file, CancellationToken ct)
    {
        current.EnsureAdmin();
        if (await db.Invoices.AnyAsync(i => i.Number == r.Number && i.Series == r.Series && i.Supplier == r.Supplier, ct))
            throw new ConflictException("Nota fiscal já cadastrada para este fornecedor.");

        var invoice = Invoice.Create(r.Number, r.Series, r.IssueDate, r.Supplier, r.TotalValue);
        string? key = null;
        if (file is not null && fileName is not null)
        {
            if (contentType is null || !AllowedTypes.Contains(contentType)) throw new InvalidInputException("Anexe um PDF ou XML.");
            key = await storage.SaveAsync("invoices", fileName, file, ct);
            invoice.AttachFile(key, Path.GetFileName(fileName), contentType);
        }
        db.Invoices.Add(invoice);
        try { await db.SaveChangesAsync(ct); }
        catch { if (key is not null) await storage.DeleteAsync(key, CancellationToken.None); throw; }
        return ToDto(invoice);
    }

    public async Task<FileResult> DownloadAsync(Guid id, CancellationToken ct)
    {
        current.EnsureAdmin();
        var i = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Nota fiscal não encontrada.");
        if (i.FileStorageKey is null) throw new NotFoundException("Esta nota não possui arquivo.");
        return new FileResult(await storage.OpenReadAsync(i.FileStorageKey, ct), i.FileContentType ?? "application/octet-stream", i.FileName ?? "nota");
    }

    private static InvoiceDto ToDto(Invoice i) => new(i.Id, i.Number, i.Series, i.IssueDate, i.Supplier, i.TotalValue,
        i.FileStorageKey is not null, i.FileName);
}
