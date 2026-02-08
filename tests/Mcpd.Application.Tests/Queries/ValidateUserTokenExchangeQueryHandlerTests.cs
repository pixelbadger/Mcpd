using FluentAssertions;
using Mcpd.Application.Interfaces;
using Mcpd.Application.Queries;
using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Mcpd.Application.Tests.Queries;

public sealed class ValidateUserTokenExchangeQueryHandlerTests
{
    private readonly IUserTokenValidator _userTokenValidator = Substitute.For<IUserTokenValidator>();
    private readonly IUserServerAuthorizationService _authService = Substitute.For<IUserServerAuthorizationService>();
    private readonly IMcpServerRepository _serverRepo = Substitute.For<IMcpServerRepository>();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly ValidateUserTokenExchangeQueryHandler _handler;

    private static readonly Guid ServerId = Guid.NewGuid();

    public ValidateUserTokenExchangeQueryHandlerTests()
    {
        _handler = new ValidateUserTokenExchangeQueryHandler(
            _userTokenValidator, _authService, _serverRepo, _tokenGenerator);
    }

    [Fact]
    public async Task ValidToken_WithAuthorizedUser_ReturnsAccessToken()
    {
        var server = new McpServer("code-assist", "Test server", new Uri("https://mcp.test.com"));
        SetupValidToken("user-123", "jane@contoso.com", ["Mcpd.Server.CodeAssist"]);
        _serverRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(server);
        _authService.Authorize("code-assist", Arg.Any<string[]>(), Arg.Any<string[]?>())
            .Returns(new UserServerAuthorizationResult(true, ["read", "write"], null));
        _tokenGenerator.GenerateUserAccessToken(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Guid>(),
            Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TimeSpan>())
            .Returns("test-jwt-token");

        var query = new ValidateUserTokenExchangeQuery("valid-assertion", ServerId, null);
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        result.IsAuthorized.Should().BeTrue();
        result.AccessToken.Should().Be("test-jwt-token");
        result.GrantedScopes.Should().BeEquivalentTo(["read", "write"]);
        result.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task InvalidUserToken_ReturnsInvalidGrant()
    {
        _userTokenValidator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserTokenValidationResult(false, null, null, [], "Token expired."));

        var query = new ValidateUserTokenExchangeQuery("expired-token", ServerId, null);
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        result.IsAuthorized.Should().BeFalse();
        result.Error.Should().Be("invalid_grant");
        result.ErrorDescription.Should().Contain("expired");
    }

    [Fact]
    public async Task InactiveServer_ReturnsInvalidTarget()
    {
        SetupValidToken("user-123", "jane@contoso.com", ["Mcpd.Server.CodeAssist"]);
        _serverRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((McpServer?)null);

        var query = new ValidateUserTokenExchangeQuery("valid-assertion", ServerId, null);
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        result.IsAuthorized.Should().BeFalse();
        result.Error.Should().Be("invalid_target");
    }

    [Fact]
    public async Task UserLacksRequiredRole_ReturnsUnauthorizedClient()
    {
        var server = new McpServer("code-assist", "Test server", new Uri("https://mcp.test.com"));
        SetupValidToken("user-456", "bob@contoso.com", ["Mcpd.Server.Other"]);
        _serverRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(server);
        _authService.Authorize("code-assist", Arg.Any<string[]>(), Arg.Any<string[]?>())
            .Returns(new UserServerAuthorizationResult(false, [], "User does not have the required role for this server."));

        var query = new ValidateUserTokenExchangeQuery("valid-assertion", ServerId, null);
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        result.IsAuthorized.Should().BeFalse();
        result.Error.Should().Be("unauthorized_client");
    }

    [Fact]
    public async Task ExcessScopes_ReturnsUnauthorizedClient()
    {
        var server = new McpServer("code-assist", "Test server", new Uri("https://mcp.test.com"));
        SetupValidToken("user-123", "jane@contoso.com", ["Mcpd.Server.CodeAssist"]);
        _serverRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(server);
        _authService.Authorize("code-assist", Arg.Any<string[]>(), Arg.Is<string[]?>(s => s != null && s.Contains("admin")))
            .Returns(new UserServerAuthorizationResult(false, [], "Requested scopes exceed allowed scopes for this server."));

        var query = new ValidateUserTokenExchangeQuery("valid-assertion", ServerId, ["read", "admin"]);
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        result.IsAuthorized.Should().BeFalse();
        result.Error.Should().Be("unauthorized_client");
    }

    [Fact]
    public async Task ValidToken_IssuesTokenWithUserSubject()
    {
        var server = new McpServer("code-assist", "Test server", new Uri("https://mcp.test.com"));
        SetupValidToken("user-789", "alice@contoso.com", ["Mcpd.Server.CodeAssist"]);
        _serverRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(server);
        _authService.Authorize("code-assist", Arg.Any<string[]>(), Arg.Any<string[]?>())
            .Returns(new UserServerAuthorizationResult(true, ["read"], null));
        _tokenGenerator.GenerateUserAccessToken(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Guid>(),
            Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TimeSpan>())
            .Returns("user-jwt");

        var query = new ValidateUserTokenExchangeQuery("valid-assertion", ServerId, ["read"]);
        await _handler.HandleAsync(query, CancellationToken.None);

        _tokenGenerator.Received(1).GenerateUserAccessToken(
            "user-789", "alice@contoso.com", Arg.Any<Guid>(), "code-assist",
            Arg.Is<string[]>(s => s.Contains("read")), Arg.Any<TimeSpan>());
    }

    private void SetupValidToken(string subject, string username, string[] claims)
    {
        _userTokenValidator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserTokenValidationResult(true, subject, username, claims, null));
    }
}
