using FluentAssertions;
using Mcpd.Infrastructure.Configuration;
using Mcpd.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mcpd.Infrastructure.Tests.Services;

public sealed class UserServerAuthorizationServiceTests
{
    private static UserServerAuthorizationService CreateService(
        Dictionary<string, ServerClaimMapping>? mappings = null,
        string adminRole = "Mcpd.Admin")
    {
        var options = Options.Create(new AuthServerOptions
        {
            AdminRole = adminRole,
            ServerMappings = mappings ?? new Dictionary<string, ServerClaimMapping>
            {
                ["code-assist"] = new ServerClaimMapping
                {
                    RequiredRoles = ["Mcpd.Server.CodeAssist", "Mcpd.AllServers"],
                    DefaultScopes = ["read", "write"]
                },
                ["data-pipeline"] = new ServerClaimMapping
                {
                    RequiredRoles = ["Mcpd.Server.DataPipeline"],
                    DefaultScopes = ["read"]
                }
            }
        });
        return new UserServerAuthorizationService(options);
    }

    [Fact]
    public void UserWithRequiredRole_IsAuthorized()
    {
        var svc = CreateService();
        var result = svc.Authorize("code-assist", ["Mcpd.Server.CodeAssist"], null);

        result.IsAuthorized.Should().BeTrue();
        result.GrantedScopes.Should().BeEquivalentTo(["read", "write"]);
    }

    [Fact]
    public void UserWithAlternateRole_IsAuthorized()
    {
        var svc = CreateService();
        var result = svc.Authorize("code-assist", ["Mcpd.AllServers"], null);

        result.IsAuthorized.Should().BeTrue();
        result.GrantedScopes.Should().BeEquivalentTo(["read", "write"]);
    }

    [Fact]
    public void UserWithoutRequiredRole_IsNotAuthorized()
    {
        var svc = CreateService();
        var result = svc.Authorize("code-assist", ["Mcpd.Server.DataPipeline"], null);

        result.IsAuthorized.Should().BeFalse();
        result.Error.Should().Contain("required role");
    }

    [Fact]
    public void RequestedScopesSubset_ReturnsRequestedScopes()
    {
        var svc = CreateService();
        var result = svc.Authorize("code-assist", ["Mcpd.Server.CodeAssist"], ["read"]);

        result.IsAuthorized.Should().BeTrue();
        result.GrantedScopes.Should().BeEquivalentTo(["read"]);
    }

    [Fact]
    public void RequestedScopesExceedDefault_ReturnsError()
    {
        var svc = CreateService();
        var result = svc.Authorize("code-assist", ["Mcpd.Server.CodeAssist"], ["read", "admin"]);

        result.IsAuthorized.Should().BeFalse();
        result.Error.Should().Contain("exceed");
    }

    [Fact]
    public void NoRequestedScopes_ReturnsDefaultScopes()
    {
        var svc = CreateService();
        var result = svc.Authorize("data-pipeline", ["Mcpd.Server.DataPipeline"], null);

        result.IsAuthorized.Should().BeTrue();
        result.GrantedScopes.Should().BeEquivalentTo(["read"]);
    }

    [Fact]
    public void UnknownServer_ReturnsError()
    {
        var svc = CreateService();
        var result = svc.Authorize("unknown-server", ["Mcpd.Server.CodeAssist"], null);

        result.IsAuthorized.Should().BeFalse();
        result.Error.Should().Contain("No claim mapping");
    }

    [Fact]
    public void EmptyRequiredRoles_AllowsAnyAuthenticatedUser()
    {
        var svc = CreateService(new Dictionary<string, ServerClaimMapping>
        {
            ["open-server"] = new ServerClaimMapping
            {
                RequiredRoles = [],
                DefaultScopes = ["read"]
            }
        });
        var result = svc.Authorize("open-server", [], null);

        result.IsAuthorized.Should().BeTrue();
        result.GrantedScopes.Should().BeEquivalentTo(["read"]);
    }

    [Fact]
    public void RoleMatchingIsCaseInsensitive()
    {
        var svc = CreateService();
        var result = svc.Authorize("code-assist", ["mcpd.server.codeassist"], null);

        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_WithAdminRole_ReturnsTrue()
    {
        var svc = CreateService();
        svc.IsAdmin(["Mcpd.Admin", "Mcpd.Server.CodeAssist"]).Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_WithoutAdminRole_ReturnsFalse()
    {
        var svc = CreateService();
        svc.IsAdmin(["Mcpd.Server.CodeAssist"]).Should().BeFalse();
    }

    [Fact]
    public void IsAdmin_CaseInsensitive()
    {
        var svc = CreateService();
        svc.IsAdmin(["mcpd.admin"]).Should().BeTrue();
    }
}
