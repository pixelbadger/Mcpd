namespace Mcpd.Domain.Entities;

public sealed class McpServer
{
    private readonly List<CallbackWhitelistEntry> _callbackWhitelist = [];
    private readonly List<ClientServerGrant> _clientGrants = [];

    private McpServer() { }

    public McpServer(string name, string description, Uri baseUri)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        BaseUri = baseUri;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Uri BaseUri { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    public IReadOnlyCollection<CallbackWhitelistEntry> CallbackWhitelist => _callbackWhitelist.AsReadOnly();
    public IReadOnlyCollection<ClientServerGrant> ClientGrants => _clientGrants.AsReadOnly();

    public void Deactivate()
    {
        IsActive = false;
        DeactivatedAt = DateTimeOffset.UtcNow;
    }

    public void AddCallbackWhitelistEntry(string pattern)
    {
        _callbackWhitelist.Add(new CallbackWhitelistEntry(Id, pattern));
    }
}
