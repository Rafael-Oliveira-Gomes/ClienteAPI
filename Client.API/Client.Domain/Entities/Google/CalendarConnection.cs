namespace Client.Domain.Entities.Google;

public class CalendarConnection
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Provider { get; private set; } = "Google";

    public string CalendarId { get; private set; } = "primary";

    public string AccessToken { get; private set; } = null!;

    public string RefreshToken { get; private set; } = null!;

    public DateTime TokenExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }
}
