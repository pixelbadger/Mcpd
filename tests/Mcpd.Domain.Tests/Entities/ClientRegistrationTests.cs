using FluentAssertions;
using Mcpd.Domain.Entities;
using Mcpd.Domain.Enums;
using Xunit;

namespace Mcpd.Domain.Tests.Entities;

public sealed class ClientRegistrationTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var reg = new ClientRegistration(
            "client-123", "hashed-secret", "Test Client",
            "client_secret_post", ["client_credentials"],
            ["https://example.com/callback"], "hashed-rat", ["read", "write"]);

        reg.ClientId.Should().Be("client-123");
        reg.ClientName.Should().Be("Test Client");
        reg.Status.Should().Be(ClientStatus.Active);
        reg.TokenEndpointAuthMethod.Should().Be("client_secret_post");
        reg.GrantTypes.Should().ContainSingle("client_credentials");
        reg.RedirectUris.Should().ContainSingle("https://example.com/callback");
        reg.Scope.Should().BeEquivalentTo(["read", "write"]);
        reg.RegistrationAccessToken.Should().Be("hashed-rat");
        reg.Id.Should().NotBeEmpty();
        reg.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Revoke_SetsStatusToRevoked()
    {
        var reg = CreateActiveRegistration();
        reg.Revoke();
        reg.Status.Should().Be(ClientStatus.Revoked);
    }

    [Fact]
    public void Suspend_SetsStatusToSuspended()
    {
        var reg = CreateActiveRegistration();
        reg.Suspend();
        reg.Status.Should().Be(ClientStatus.Suspended);
    }

    [Fact]
    public void RotateSecret_UpdatesHashAndTimestamp()
    {
        var reg = CreateActiveRegistration();
        var newExpiry = DateTimeOffset.UtcNow.AddDays(90);

        reg.RotateSecret("new-hashed-secret", newExpiry);

        reg.ClientSecretHash.Should().Be("new-hashed-secret");
        reg.SecretExpiresAt.Should().Be(newExpiry);
        reg.SecretRotatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateMetadata_UpdatesFields()
    {
        var reg = CreateActiveRegistration();

        reg.UpdateMetadata("New Name", ["https://new.com/cb"], "client_secret_basic", ["client_credentials"], ["admin"]);

        reg.ClientName.Should().Be("New Name");
        reg.RedirectUris.Should().ContainSingle("https://new.com/cb");
        reg.TokenEndpointAuthMethod.Should().Be("client_secret_basic");
        reg.GrantTypes.Should().ContainSingle("client_credentials");
        reg.Scope.Should().ContainSingle("admin");
    }

    private static ClientRegistration CreateActiveRegistration() =>
        new("client-1", "hash", "Test", "client_secret_post",
            ["client_credentials"], ["https://example.com/cb"], "rat-hash", ["read"]);
}
