namespace Mcpd.Infrastructure.Configuration;

public sealed class AuthServerOptions
{
    public const string SectionName = "AuthServer";

    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string? MetadataUrl { get; set; }
    public string ServerAccessClaimType { get; set; } = "roles";
    public string AdminRole { get; set; } = "Mcpd.Admin";
    public Dictionary<string, ServerClaimMapping> ServerMappings { get; set; } = new();
}

public sealed class ServerClaimMapping
{
    public string[] RequiredRoles { get; set; } = [];
    public string[] DefaultScopes { get; set; } = [];
}
