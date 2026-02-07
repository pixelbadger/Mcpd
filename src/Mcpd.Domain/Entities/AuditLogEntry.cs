namespace Mcpd.Domain.Entities;

public sealed class AuditLogEntry
{
    private AuditLogEntry() { }

    public AuditLogEntry(string action, string actorId, Guid? clientRegistrationId, Guid? mcpServerId, string? detail)
    {
        Id = Guid.NewGuid();
        Action = action;
        ActorId = actorId;
        ClientRegistrationId = clientRegistrationId;
        McpServerId = mcpServerId;
        Detail = detail;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Action { get; private set; } = null!;
    public string ActorId { get; private set; } = null!;
    public Guid? ClientRegistrationId { get; private set; }
    public Guid? McpServerId { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
}
