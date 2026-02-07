using FluentAssertions;
using Mcpd.Domain.Entities;
using Xunit;

namespace Mcpd.Domain.Tests.Entities;

public sealed class McpServerTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var server = new McpServer("code-assist", "Code gen server", new Uri("https://mcp.example.com"));

        server.Name.Should().Be("code-assist");
        server.Description.Should().Be("Code gen server");
        server.BaseUri.Should().Be(new Uri("https://mcp.example.com"));
        server.IsActive.Should().BeTrue();
        server.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        var server = new McpServer("test", "test", new Uri("https://test.com"));
        server.Deactivate();

        server.IsActive.Should().BeFalse();
        server.DeactivatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddCallbackWhitelistEntry_AddsEntry()
    {
        var server = new McpServer("test", "test", new Uri("https://test.com"));
        server.AddCallbackWhitelistEntry("https://app.example.com/callback");

        server.CallbackWhitelist.Should().HaveCount(1);
        server.CallbackWhitelist.First().Pattern.Should().Be("https://app.example.com/callback");
    }
}
