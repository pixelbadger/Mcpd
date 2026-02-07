namespace Mcpd.Infrastructure.Configuration;

public sealed class McpServerConfig
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BaseUri { get; set; } = string.Empty;
    public string[] CallbackWhitelist { get; set; } = [];
}
