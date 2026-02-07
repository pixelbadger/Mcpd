namespace Mcpd.Domain.Entities;

public sealed class ClientServerGrant
{
    private ClientServerGrant() { }

    public ClientServerGrant(Guid clientRegistrationId, Guid mcpServerId, string[] scopes)
    {
        Id = Guid.NewGuid();
        ClientRegistrationId = clientRegistrationId;
        McpServerId = mcpServerId;
        Scopes = scopes;
        IsActive = true;
        GrantedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ClientRegistrationId { get; private set; }
    public Guid McpServerId { get; private set; }
    public string[] Scopes { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public ClientRegistration Client { get; private set; } = null!;
    public McpServer Server { get; private set; } = null!;

    public void Revoke()
    {
        IsActive = false;
        RevokedAt = DateTimeOffset.UtcNow;
    }
}
