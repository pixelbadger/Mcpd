using FluentAssertions;
using Mcpd.Domain.Entities;
using Xunit;

namespace Mcpd.Domain.Tests.Entities;

public sealed class ClientServerGrantTests
{
    [Fact]
    public void Constructor_CreatesActiveGrant()
    {
        var clientId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var grant = new ClientServerGrant(clientId, serverId, ["read", "write"]);

        grant.ClientRegistrationId.Should().Be(clientId);
        grant.McpServerId.Should().Be(serverId);
        grant.Scopes.Should().BeEquivalentTo(["read", "write"]);
        grant.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Revoke_DeactivatesGrant()
    {
        var grant = new ClientServerGrant(Guid.NewGuid(), Guid.NewGuid(), ["read"]);
        grant.Revoke();

        grant.IsActive.Should().BeFalse();
        grant.RevokedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
