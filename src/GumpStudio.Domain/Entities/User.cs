using GumpStudio.Domain.Common;
using GumpStudio.Domain.Enums;

namespace GumpStudio.Domain.Entities;

/// <summary>Usuário autenticado via Google (e-mail). Não há senha local.</summary>
public sealed class User : AuditableEntity
{
    private User() { }

    public string Email { get; private set; } = "";
    public string Name { get; private set; } = "";
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? GoogleSubject { get; private set; }
    public DateTimeOffset? AccessStartsAt { get; private set; }
    public DateTimeOffset? AccessExpiresAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public static User Create(string email, string name, UserRole role,
        DateTimeOffset? accessStartsAt = null, DateTimeOffset? accessExpiresAt = null)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("E-mail inválido.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome é obrigatório.");

        var user = new User { Email = email.Trim().ToLowerInvariant(), Name = name.Trim(), Role = role };
        user.SetAccessWindow(accessStartsAt, accessExpiresAt);
        return user;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome é obrigatório.");
        Name = name.Trim();
    }

    /// <summary>Janela de acesso (tatuador rotativo, seção 3.3).</summary>
    public void SetAccessWindow(DateTimeOffset? startsAt, DateTimeOffset? expiresAt)
    {
        if (startsAt.HasValue && expiresAt.HasValue && expiresAt <= startsAt)
            throw new DomainException("A data de expiração deve ser posterior à data de início.");
        AccessStartsAt = startsAt?.ToUniversalTime();
        AccessExpiresAt = expiresAt?.ToUniversalTime();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public AccessStatus GetAccessStatus(DateTimeOffset now)
    {
        if (!IsActive) return AccessStatus.Inactive;
        if (AccessStartsAt.HasValue && now < AccessStartsAt) return AccessStatus.NotStarted;
        if (AccessExpiresAt.HasValue && now >= AccessExpiresAt) return AccessStatus.Expired;
        return AccessStatus.Allowed;
    }

    public void EnsureCanAuthenticate(DateTimeOffset now)
    {
        switch (GetAccessStatus(now))
        {
            case AccessStatus.Inactive: throw new AccessDeniedException("Usuário desativado.");
            case AccessStatus.NotStarted: throw new AccessDeniedException("O acesso ainda não foi liberado.");
            case AccessStatus.Expired: throw new AccessDeniedException("O acesso temporário expirou.");
        }
    }

    public void RegisterLogin(string? googleSubject, DateTimeOffset now)
    {
        if (googleSubject is not null) GoogleSubject ??= googleSubject;
        LastLoginAt = now;
    }
}

public sealed class RefreshToken : Entity
{
    private RefreshToken() { }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new() { UserId = userId, TokenHash = tokenHash, CreatedAt = now, ExpiresAt = now + lifetime };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
