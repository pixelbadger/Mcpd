namespace Mcpd.Application.Contracts;

public sealed record ClientRegistrationResponse(
    string ClientId,
    string? ClientSecret,
    string ClientName,
    string[] RedirectUris,
    string[] GrantTypes,
    string TokenEndpointAuthMethod,
    string[] Scope,
    string? RegistrationAccessToken,
    DateTimeOffset? ClientSecretExpiresAt);

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string[] Scope);

public sealed record TokenErrorResponse(
    string Error,
    string? ErrorDescription);
