using Mcpd.Application.Contracts;
using Mcpd.Application.Interfaces;
using Mcpd.Domain.Enums;
using Mcpd.Domain.Interfaces;
using Mcpd.Domain.ValueObjects;
using Mediator;

namespace Mcpd.Application.Queries;

public sealed record ValidateTokenRequestQuery(
    string ClientId,
    string ClientSecret,
    Guid ServerId,
    string[]? RequestedScopes,
    string AuthMethod) : IQuery<TokenValidationResult>;

public sealed record TokenValidationResult(
    bool IsAuthorized,
    string? Error,
    string? ErrorDescription,
    string? AccessToken,
    string[]? GrantedScopes,
    int? ExpiresIn);

public sealed class ValidateTokenRequestQueryHandler(
    IClientRegistrationRepository clientRepo,
    IClientServerGrantRepository grantRepo,
    IMcpServerRepository serverRepo,
    ISecretHasher secretHasher,
    ITokenGenerator tokenGenerator) : IQueryHandler<ValidateTokenRequestQuery, TokenValidationResult>
{
    private static readonly HashedSecret DummyHash = new(
        "$argon2id$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");

    public async ValueTask<TokenValidationResult> Handle(ValidateTokenRequestQuery query, CancellationToken ct)
    {
        // Step 1: Verify client exists and is Active
        var registration = await clientRepo.GetByClientIdAsync(query.ClientId, ct);
        if (registration is null || registration.Status != ClientStatus.Active)
        {
            secretHasher.Verify("dummy", DummyHash);
            return Fail("invalid_client", "Client not found or inactive.");
        }

        // Step 2: Verify auth method matches registered method
        if (!string.Equals(registration.TokenEndpointAuthMethod, query.AuthMethod, StringComparison.Ordinal))
            return Fail("invalid_client", "Authentication method does not match registered method.");

        // Step 3: Verify secret against stored Argon2id hash
        if (!secretHasher.Verify(query.ClientSecret, new HashedSecret(registration.ClientSecretHash)))
            return Fail("invalid_client", "Invalid client credentials.");

        // Step 3: Verify active ClientServerGrant exists
        var grant = await grantRepo.GetAsync(registration.Id, query.ServerId, ct);
        if (grant is null || !grant.IsActive)
            return Fail("unauthorized_client", "Client is not authorized for the requested server.");

        // Step 4: Verify requested scopes are subset of grant's scopes
        var requestedScopes = query.RequestedScopes ?? [];
        if (requestedScopes.Length > 0 && !requestedScopes.All(s => grant.Scopes.Contains(s)))
            return Fail("invalid_scope", "Requested scopes exceed granted scopes.");

        var effectiveScopes = requestedScopes.Length > 0 ? requestedScopes : grant.Scopes;

        // Step 5: Get server info and issue token
        var server = await serverRepo.GetByIdAsync(query.ServerId, ct);
        var lifetime = TimeSpan.FromMinutes(60);
        var accessToken = tokenGenerator.GenerateAccessToken(
            query.ClientId, query.ServerId, server?.Name ?? "unknown", effectiveScopes, lifetime);

        return new TokenValidationResult(
            true, null, null, accessToken, effectiveScopes, (int)lifetime.TotalSeconds);
    }

    private static TokenValidationResult Fail(string error, string description) =>
        new(false, error, description, null, null, null);
}
