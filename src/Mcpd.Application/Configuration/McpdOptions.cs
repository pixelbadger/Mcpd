namespace Mcpd.Application.Configuration;

public sealed class McpdOptions
{
    public const string SectionName = "Mcpd";

    public string Issuer { get; set; } = "https://dcr.contoso.com";
    public string? SigningKeyPath { get; set; }
    public int DefaultTokenLifetimeMinutes { get; set; } = 60;
    public int DefaultSecretLifetimeDays { get; set; } = 90;
    public string AdminApiKey { get; set; } = string.Empty;
    public bool AllowOpenRegistration { get; set; } = true;
    public bool RequireHttpsCallbacks { get; set; } = true;
}
