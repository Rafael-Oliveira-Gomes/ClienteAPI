namespace GumpStudio.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

/// <summary>Auditoria (doc. seção 33). Preenchida automaticamente pelo DbContext.</summary>
public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class DomainException(string message) : Exception(message);

public sealed class AccessDeniedException(string message) : DomainException(message);
