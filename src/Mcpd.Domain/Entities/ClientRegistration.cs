using Mcpd.Domain.Enums;

namespace Mcpd.Domain.Entities;

public sealed class ClientRegistration
{
    private ClientRegistration() { }

    public ClientRegistration(
        string clientId,
        string clientSecretHash,
        string clientName,
        string tokenEndpointAuthMethod,
        string[] grantTypes,
        string[] redirectUris,
        string registrationAccessTokenHash,
        string[] scope)
    {
        Id = Guid.NewGuid();
        ClientId = clientId;
        ClientSecretHash = clientSecretHash;
        ClientName = clientName;
        Status = ClientStatus.Active;
        TokenEndpointAuthMethod = tokenEndpointAuthMethod;
        GrantTypes = grantTypes;
        RedirectUris = redirectUris;
        RegistrationAccessToken = registrationAccessTokenHash;
        Scope = scope;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string ClientId { get; private set; } = null!;
    public string ClientSecretHash { get; private set; } = null!;
    public string ClientName { get; private set; } = null!;
    public ClientStatus Status { get; private set; }
    public string TokenEndpointAuthMethod { get; private set; } = null!;
    public string[] GrantTypes { get; private set; } = null!;
    public string[] RedirectUris { get; private set; } = null!;
    public string[] Scope { get; private set; } = null!;
    public string? RegistrationAccessToken { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SecretExpiresAt { get; private set; }
    public DateTimeOffset? SecretRotatedAt { get; private set; }

    public void SetSecretExpiry(DateTimeOffset expiresAt)
    {
        SecretExpiresAt = expiresAt;
    }

    public void RotateSecret(string newSecretHash, DateTimeOffset? newExpiresAt)
    {
        ClientSecretHash = newSecretHash;
        SecretRotatedAt = DateTimeOffset.UtcNow;
        SecretExpiresAt = newExpiresAt;
    }

    public void UpdateMetadata(string clientName, string[] redirectUris, string tokenEndpointAuthMethod, string[] grantTypes, string[] scope)
    {
        ClientName = clientName;
        RedirectUris = redirectUris;
        TokenEndpointAuthMethod = tokenEndpointAuthMethod;
        GrantTypes = grantTypes;
        Scope = scope;
    }

    public void Revoke()
    {
        Status = ClientStatus.Revoked;
    }

    public void Suspend()
    {
        Status = ClientStatus.Suspended;
    }
}
