using FluentAssertions;
using Mcpd.Domain.ValueObjects;
using Xunit;

namespace Mcpd.Domain.Tests.ValueObjects;

public sealed class ValueObjectTests
{
    [Fact]
    public void ClientId_EqualsWithSameValue()
    {
        var a = new ClientId("abc");
        var b = new ClientId("abc");
        a.Should().Be(b);
    }

    [Fact]
    public void ClientId_ToString_ReturnsValue()
    {
        var id = new ClientId("test-client");
        id.ToString().Should().Be("test-client");
    }

    [Fact]
    public void ClientSecret_ToString_ReturnsRedacted()
    {
        var secret = new ClientSecret("super-secret");
        secret.ToString().Should().Be("***REDACTED***");
    }

    [Fact]
    public void HashedSecret_ToString_ReturnsHashed()
    {
        var hash = new HashedSecret("$argon2id$...");
        hash.ToString().Should().Be("***HASHED***");
    }

    [Fact]
    public void RegistrationToken_ToString_ReturnsRedacted()
    {
        var token = new RegistrationToken("rat-value");
        token.ToString().Should().Be("***REDACTED***");
    }
}
