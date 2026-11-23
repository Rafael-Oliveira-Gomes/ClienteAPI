using GumpStudio.Domain.Common;
using GumpStudio.Domain.Enums;

namespace GumpStudio.Domain.Entities;

public sealed class TattooSession : AuditableEntity
{
    private static readonly Dictionary<SessionStatus, SessionStatus[]> Transitions = new()
    {
        [SessionStatus.Scheduled] = [SessionStatus.Confirmed, SessionStatus.InProgress, SessionStatus.Cancelled, SessionStatus.NoShow],
        [SessionStatus.Confirmed] = [SessionStatus.InProgress, SessionStatus.Cancelled, SessionStatus.NoShow],
        [SessionStatus.InProgress] = [SessionStatus.Completed, SessionStatus.Cancelled],
        [SessionStatus.Completed] = [],
        [SessionStatus.Cancelled] = [],
        [SessionStatus.NoShow] = []
    };

    private TattooSession() { }

    public Guid ClientId { get; private set; }
    public Guid ArtistId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public decimal Value { get; private set; }
    public SessionStatus Status { get; private set; } = SessionStatus.Scheduled;
    public string? Notes { get; private set; }
    public string? Location { get; private set; }
    public string? ReferenceUrl { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CancelReason { get; private set; }

    public string? GoogleCalendarEventId { get; private set; }
    public CalendarSyncStatus CalendarSyncStatus { get; private set; }
    public string? CalendarSyncError { get; private set; }

    public bool IsEditable => Status is SessionStatus.Scheduled or SessionStatus.Confirmed;
    public bool CanRegisterUsage => Status is SessionStatus.Scheduled or SessionStatus.Confirmed or SessionStatus.InProgress;

    public static TattooSession Create(Guid clientId, Guid artistId, DateTimeOffset startsAt, int durationMinutes,
        decimal value, string? location, string? notes, string? referenceUrl)
    {
        var s = new TattooSession { ClientId = clientId, ArtistId = artistId };
        s.SetSchedule(startsAt, durationMinutes);
        s.SetDetails(value, location, notes, referenceUrl);
        return s;
    }

    public void SetSchedule(DateTimeOffset startsAt, int durationMinutes)
    {
        if (durationMinutes <= 0) throw new DomainException("A duração estimada deve ser maior que zero.");
        StartsAt = startsAt.ToUniversalTime();
        EstimatedDurationMinutes = durationMinutes;
        EndsAt = StartsAt.AddMinutes(durationMinutes);
    }

    public void SetDetails(decimal value, string? location, string? notes, string? referenceUrl)
    {
        if (value < 0) throw new DomainException("O valor da tatuagem não pode ser negativo.");
        Value = value; Location = location; Notes = notes; ReferenceUrl = referenceUrl;
    }

    /// <summary>Só é possível alterar horário/artista enquanto agendada ou confirmada.</summary>
    public void Reschedule(Guid artistId, DateTimeOffset startsAt, int durationMinutes)
    {
        if (!IsEditable) throw new DomainException("Apenas sessões agendadas ou confirmadas podem ser alteradas.");
        ArtistId = artistId;
        SetSchedule(startsAt, durationMinutes);
    }

    public void Confirm() => MoveTo(SessionStatus.Confirmed);
    public void Start() => MoveTo(SessionStatus.InProgress);
    public void MarkNoShow() => MoveTo(SessionStatus.NoShow);

    public void Complete(DateTimeOffset now)
    {
        MoveTo(SessionStatus.Completed);
        CompletedAt = now;
    }

    public void Cancel(string? reason)
    {
        MoveTo(SessionStatus.Cancelled);
        CancelReason = reason;
    }

    private void MoveTo(SessionStatus next)
    {
        if (!Transitions[Status].Contains(next))
            throw new DomainException($"Transição de status inválida: {Status} → {next}.");
        Status = next;
    }

    public void MarkCalendarSynced(string eventId)
    {
        GoogleCalendarEventId = eventId;
        CalendarSyncStatus = CalendarSyncStatus.Synced;
        CalendarSyncError = null;
    }

    public void MarkCalendarFailed(string error)
    {
        CalendarSyncStatus = CalendarSyncStatus.Failed;
        CalendarSyncError = error.Length > 500 ? error[..500] : error;
    }
}

public sealed class ExamDocument : AuditableEntity
{
    private ExamDocument() { }

    public Guid ClientId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string Type { get; private set; } = "";
    public DateOnly ExamDate { get; private set; }
    public string? Notes { get; private set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Pending;
    public string StorageKey { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";

    public static ExamDocument Create(Guid clientId, Guid? sessionId, string type, DateOnly examDate,
        string? notes, string storageKey, string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new DomainException("Informe o tipo do documento.");
        return new ExamDocument
        {
            ClientId = clientId, SessionId = sessionId, Type = type.Trim(), ExamDate = examDate,
            Notes = notes, StorageKey = storageKey, FileName = fileName, ContentType = contentType
        };
    }

    public void SetStatus(DocumentStatus status) => Status = status;
}
