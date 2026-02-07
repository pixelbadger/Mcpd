namespace Mcpd.Domain.Entities;

public sealed class CallbackWhitelistEntry
{
    private CallbackWhitelistEntry() { }

    public CallbackWhitelistEntry(Guid mcpServerId, string pattern)
    {
        Id = Guid.NewGuid();
        McpServerId = mcpServerId;
        Pattern = pattern;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid McpServerId { get; private set; }
    public string Pattern { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public McpServer Server { get; private set; } = null!;
}
