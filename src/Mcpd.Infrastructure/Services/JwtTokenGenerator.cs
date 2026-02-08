using System.Security.Cryptography;
using Mcpd.Application.Interfaces;
using Mcpd.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mcpd.Infrastructure.Services;

public sealed class JwtTokenGenerator(IOptions<McpdOptions> options, SigningKeyManager signingKeyManager) : ITokenGenerator
{
    private readonly McpdOptions _options = options.Value;

    public string GenerateClientId() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string GenerateClientSecret() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(48));

    public string GenerateRegistrationAccessToken() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string GenerateAccessToken(string clientId, string[] scopes, TimeSpan lifetime, string? audience)
    {
        var credentials = signingKeyManager.GetSigningCredentials();

        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", clientId),
            new("scope", string.Join(' ', scopes)),
            new("jti", Guid.NewGuid().ToString()),
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            Subject = new System.Security.Claims.ClaimsIdentity(claims),
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
