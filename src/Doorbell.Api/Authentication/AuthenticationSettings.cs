namespace Doorbell.Api.Authentication;

public sealed class AuthenticationSettings
{
    public const string SectionName = "Authentication";

    public JwtSettings Jwt { get; set; } = new();

    public DevelopmentAuthSettings Development { get; set; } = new();
}

public sealed class JwtSettings
{
    public string? Authority { get; set; }

    public string? Audience { get; set; }

    public string? ValidIssuer { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;

    public string? IssuerSigningKey { get; set; }
}

public sealed class DevelopmentAuthSettings
{
    public bool Enabled { get; set; }

    public string SigningKey { get; set; } =
        "doorbell-development-signing-key-change-me-0123456789abcdef";

    public int TokenLifetimeMinutes { get; set; } = 120;
}
