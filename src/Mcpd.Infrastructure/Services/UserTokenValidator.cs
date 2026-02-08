using Mcpd.Application.Interfaces;
using Mcpd.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Mcpd.Infrastructure.Services;

public sealed class UserTokenValidator : IUserTokenValidator
{
    private readonly AuthServerOptions _options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _configManager;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public UserTokenValidator(IOptions<AuthServerOptions> options)
    {
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.Authority))
        {
            var metadataUrl = _options.MetadataUrl
                ?? $"{_options.Authority.TrimEnd('/')}/.well-known/openid-configuration";

            _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataUrl,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());
        }
    }

    public async Task<UserTokenValidationResult> ValidateAsync(string token, CancellationToken ct)
    {
        if (_configManager is null)
            return new UserTokenValidationResult(false, null, null, [], "Auth server is not configured.");

        OpenIdConnectConfiguration config;
        try
        {
            config = await _configManager.GetConfigurationAsync(ct);
        }
        catch (Exception ex)
        {
            return new UserTokenValidationResult(false, null, null, [],
                $"Failed to retrieve auth server metadata: {ex.Message}");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = _options.Authority,
            ValidAudience = _options.Audience,
            IssuerSigningKeys = config.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var result = await _tokenHandler.ValidateTokenAsync(token, validationParameters);

        if (!result.IsValid)
            return new UserTokenValidationResult(false, null, null, [],
                result.Exception?.Message ?? "Token validation failed.");

        var subject = result.ClaimsIdentity.FindFirst("sub")?.Value
            ?? result.ClaimsIdentity.FindFirst("oid")?.Value;

        var preferredUsername = result.ClaimsIdentity.FindFirst("preferred_username")?.Value
            ?? result.ClaimsIdentity.FindFirst("name")?.Value;

        var claims = result.ClaimsIdentity
            .FindAll(_options.ServerAccessClaimType)
            .Select(c => c.Value)
            .ToArray();

        return new UserTokenValidationResult(true, subject, preferredUsername, claims, null);
    }
}
