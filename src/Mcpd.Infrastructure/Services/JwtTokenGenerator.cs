using System.Security.Cryptography;
using System.Text;
using Mcpd.Application.Interfaces;
using Mcpd.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mcpd.Infrastructure.Services;

public sealed class JwtTokenGenerator(IOptions<McpdOptions> options) : ITokenGenerator
{
    private readonly McpdOptions _options = options.Value;

    public string GenerateClientId() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string GenerateClientSecret() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(48));

    public string GenerateRegistrationAccessToken() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string GenerateAccessToken(string clientId, Guid serverId, string serverName, string[] scopes, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.TokenSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = serverName,
            Subject = new System.Security.Claims.ClaimsIdentity(
            [
                new("sub", clientId),
                new("server_id", serverId.ToString()),
                new("scope", string.Join(' ', scopes)),
                new("jti", Guid.NewGuid().ToString()),
            ]),
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }

    public string GenerateUserAccessToken(string userSubject, string? preferredUsername, Guid serverId, string serverName, string[] scopes, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.TokenSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", userSubject),
            new("server_id", serverId.ToString()),
            new("scope", string.Join(' ', scopes)),
            new("jti", Guid.NewGuid().ToString()),
            new("token_type", "user"),
        };

        if (!string.IsNullOrWhiteSpace(preferredUsername))
            claims.Add(new("preferred_username", preferredUsername));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = serverName,
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
