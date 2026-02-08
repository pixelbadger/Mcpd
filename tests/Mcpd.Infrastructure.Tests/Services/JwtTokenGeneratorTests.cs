using FluentAssertions;
using Mcpd.Infrastructure.Configuration;
using Xunit;
using Mcpd.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mcpd.Infrastructure.Tests.Services;

public sealed class JwtTokenGeneratorTests : IDisposable
{
    private readonly JwtTokenGenerator _generator;
    private readonly McpdOptions _options;
    private readonly SigningKeyManager _signingKeyManager;

    public JwtTokenGeneratorTests()
    {
        _options = new McpdOptions
        {
            Issuer = "https://test-dcr.example.com"
        };
        _signingKeyManager = new SigningKeyManager();
        _generator = new JwtTokenGenerator(Options.Create(_options), _signingKeyManager);
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
        var validationResult = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = "code-assist",
            IssuerSigningKey = _signingKeyManager.SecurityKey,
            ValidateLifetime = true
        });

        validationResult.IsValid.Should().BeTrue();
        validationResult.Claims.Should().ContainKey("sub");
        validationResult.Claims["sub"].Should().Be("client-1");
        validationResult.Claims.Should().ContainKey("server_id");
        validationResult.Claims["server_id"].Should().Be(serverId.ToString());
    }

    public void Dispose() => _signingKeyManager.Dispose();
}
