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
        var token = _generator.GenerateAccessToken("client-1", ["read", "write"], TimeSpan.FromMinutes(60), "code-assist");

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
    }

    [Fact]
    public async Task GenerateAccessToken_WithoutAudience_Succeeds()
    {
        var token = _generator.GenerateAccessToken("client-1", ["read"], TimeSpan.FromMinutes(60), null);

        token.Should().NotBeNullOrWhiteSpace();

        var handler = new JsonWebTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidateAudience = false,
            IssuerSigningKey = _signingKeyManager.SecurityKey,
            ValidateLifetime = true
        });

        validationResult.IsValid.Should().BeTrue();
    }

    public void Dispose() => _signingKeyManager.Dispose();
}
