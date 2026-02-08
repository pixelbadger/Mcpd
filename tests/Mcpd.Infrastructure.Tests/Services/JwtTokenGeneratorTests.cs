using FluentAssertions;
using Mcpd.Infrastructure.Configuration;
using Xunit;
using Mcpd.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Mcpd.Infrastructure.Tests.Services;

public sealed class JwtTokenGeneratorTests
{
    private readonly JwtTokenGenerator _generator;
    private readonly McpdOptions _options;

    public JwtTokenGeneratorTests()
    {
        _options = new McpdOptions
        {
            Issuer = "https://test-dcr.example.com",
            TokenSigningKey = "test-signing-key-that-is-at-least-32-characters-long!!"
        };
        _generator = new JwtTokenGenerator(Options.Create(_options));
    }

    [Fact]
    public void GenerateClientId_ReturnsUrlSafeBase64()
    {
        var id = _generator.GenerateClientId();
        id.Should().NotBeNullOrWhiteSpace();
        id.Should().NotContain("+");
        id.Should().NotContain("/");
        id.Should().NotContain("=");
    }

    [Fact]
    public void GenerateClientSecret_ReturnsUniqueValues()
    {
        var s1 = _generator.GenerateClientSecret();
        var s2 = _generator.GenerateClientSecret();
        s1.Should().NotBe(s2);
    }

    [Fact]
    public async Task GenerateAccessToken_ContainsExpectedClaims()
    {
        var serverId = Guid.NewGuid();
        var token = _generator.GenerateAccessToken("client-1", serverId, "code-assist", ["read", "write"], TimeSpan.FromMinutes(60));

        token.Should().NotBeNullOrWhiteSpace();

        var handler = new JsonWebTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.TokenSigningKey));
        var validationResult = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = "code-assist",
            IssuerSigningKey = key,
            ValidateLifetime = true
        });

        validationResult.IsValid.Should().BeTrue();
        validationResult.Claims.Should().ContainKey("sub");
        validationResult.Claims["sub"].Should().Be("client-1");
        validationResult.Claims.Should().ContainKey("server_id");
        validationResult.Claims["server_id"].Should().Be(serverId.ToString());
    }

    [Fact]
    public async Task GenerateUserAccessToken_ContainsUserClaims()
    {
        var serverId = Guid.NewGuid();
        var token = _generator.GenerateUserAccessToken(
            "user-sub-123", "alice@contoso.com", serverId, "code-assist", ["read"], TimeSpan.FromMinutes(60));

        token.Should().NotBeNullOrWhiteSpace();

        var handler = new JsonWebTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.TokenSigningKey));
        var validationResult = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = "code-assist",
            IssuerSigningKey = key,
            ValidateLifetime = true
        });

        validationResult.IsValid.Should().BeTrue();
        validationResult.Claims["sub"].Should().Be("user-sub-123");
        validationResult.Claims["preferred_username"].Should().Be("alice@contoso.com");
        validationResult.Claims["token_type"].Should().Be("user");
        validationResult.Claims["server_id"].Should().Be(serverId.ToString());
        validationResult.Claims["scope"].Should().Be("read");
    }

    [Fact]
    public async Task GenerateUserAccessToken_WithoutUsername_OmitsPreferredUsername()
    {
        var serverId = Guid.NewGuid();
        var token = _generator.GenerateUserAccessToken(
            "user-sub-456", null, serverId, "data-pipeline", ["read"], TimeSpan.FromMinutes(30));

        var handler = new JsonWebTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.TokenSigningKey));
        var validationResult = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = "data-pipeline",
            IssuerSigningKey = key,
            ValidateLifetime = true
        });

        validationResult.IsValid.Should().BeTrue();
        validationResult.Claims["sub"].Should().Be("user-sub-456");
        validationResult.Claims.Should().NotContainKey("preferred_username");
    }
}
