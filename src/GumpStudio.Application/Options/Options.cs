namespace GumpStudio.Application.Options;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "GumpStudio";
    public string Audience { get; set; } = "GumpStudio";
    /// <summary>Mínimo de 32 caracteres. Em produção, via variável de ambiente/secret.</summary>
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class GoogleOptions
{
    public const string Section = "Google";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    /// <summary>URL registrada no Google Cloud Console para o callback do Calendar (ex.: https://api.../api/calendar/callback).</summary>
    public string CalendarRedirectUri { get; set; } = "";
    /// <summary>Para onde o navegador volta após conectar o Google Agenda (front). Opcional.</summary>
    public string? CalendarPostConnectRedirect { get; set; }
    public string CalendarId { get; set; } = "primary";
    public string TimeZone { get; set; } = "America/Sao_Paulo";
}

public sealed class AuthOptions
{
    public const string Section = "Auth";
    /// <summary>E-mail Google do primeiro administrador. Só vale enquanto não existir nenhum usuário.</summary>
    public string? BootstrapAdminEmail { get; set; }
}

public sealed class StorageOptions
{
    public const string Section = "Storage";
    public string RootPath { get; set; } = "storage";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
