using Mcpd.Application.Interfaces;
using Mcpd.Domain.Interfaces;

namespace Mcpd.Application.Queries;

public sealed record ValidateUserTokenExchangeQuery(
    string Assertion,
    Guid ServerId,
    string[]? RequestedScopes);

public sealed class ValidateUserTokenExchangeQueryHandler(
    IUserTokenValidator userTokenValidator,
    IUserServerAuthorizationService authorizationService,
    IMcpServerRepository serverRepo,
    ITokenGenerator tokenGenerator)
{
    public async Task<TokenValidationResult> HandleAsync(ValidateUserTokenExchangeQuery query, CancellationToken ct)
    {
        // Step 1: Validate the user's IdP token
        var validationResult = await userTokenValidator.ValidateAsync(query.Assertion, ct);
        if (!validationResult.IsValid)
            return Fail("invalid_grant", validationResult.Error ?? "User token validation failed.");

        // Step 2: Look up the target MCP server
        var server = await serverRepo.GetByIdAsync(query.ServerId, ct);
        if (server is null || !server.IsActive)
            return Fail("invalid_target", "Target server not found or inactive.");

        // Step 3: Check claim-based authorization for this server
        var authResult = authorizationService.Authorize(server.Name, validationResult.Claims, query.RequestedScopes);
        if (!authResult.IsAuthorized)
            return Fail("unauthorized_client", authResult.Error ?? "User is not authorized for the requested server.");

        // Step 4: Issue MCP-scoped token
        var lifetime = TimeSpan.FromMinutes(60);
        var accessToken = tokenGenerator.GenerateUserAccessToken(
            validationResult.Subject!,
            validationResult.PreferredUsername,
            query.ServerId,
            server.Name,
            authResult.GrantedScopes,
            lifetime);

        return new TokenValidationResult(
            true, null, null, accessToken, authResult.GrantedScopes, (int)lifetime.TotalSeconds);
    }

    private static TokenValidationResult Fail(string error, string description) =>
        new(false, error, description, null, null, null);
}
