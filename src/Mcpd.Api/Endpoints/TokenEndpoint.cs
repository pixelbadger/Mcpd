using System.Text;
using FastEndpoints;
using Mcpd.Application.Contracts;
using Mcpd.Application.Queries;

namespace Mcpd.Api.Endpoints;

public sealed class TokenEndpoint(
    ValidateTokenRequestQueryHandler clientCredentialsHandler,
    ValidateUserTokenExchangeQueryHandler userTokenHandler)
    : EndpointWithoutRequest
{
    private const string JwtBearerGrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";

    public override void Configure()
    {
        Post("/token");
        AllowAnonymous();
        Description(x => x.WithName("Token"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? grantType = null;
        string? clientId = null;
        string? clientSecret = null;
        string? serverId = null;
        string? scope = null;
        string? assertion = null;

        if (HttpContext.Request.HasFormContentType)
        {
            var form = await HttpContext.Request.ReadFormAsync(ct);
            grantType = form["grant_type"].ToString();
            clientId = form["client_id"].ToString();
            clientSecret = form["client_secret"].ToString();
            serverId = form["server_id"].ToString();
            assertion = form["assertion"].ToString();
            var scopeValues = form["scope"];
            scope = scopeValues.Count > 1
                ? string.Join(" ", scopeValues!)
                : scopeValues.ToString();
        }

        if (string.IsNullOrWhiteSpace(grantType))
        {
            await SendAsync(new TokenErrorResponse("invalid_request", "grant_type is required."), 400, ct);
            return;
        }

        if (grantType == "client_credentials")
        {
            await HandleClientCredentialsAsync(clientId, clientSecret, serverId, scope, ct);
            return;
        }

        if (grantType == JwtBearerGrantType)
        {
            await HandleJwtBearerAsync(assertion, serverId, scope, ct);
            return;
        }

        await SendAsync(new TokenErrorResponse("unsupported_grant_type",
            "Supported grant types: client_credentials, urn:ietf:params:oauth:grant-type:jwt-bearer."), 400, ct);
    }

    private async Task HandleClientCredentialsAsync(
        string? clientId, string? clientSecret, string? serverId, string? scope, CancellationToken ct)
    {
        // Extract credentials from Basic auth header if not in body
        var authMethod = "client_secret_post";
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = Encoding.UTF8.GetString(
                Convert.FromBase64String(authHeader["Basic ".Length..]));
            var parts = decoded.Split(':', 2);
            if (parts.Length == 2)
            {
                clientId = parts[0];
                clientSecret = parts[1];
                authMethod = "client_secret_basic";
            }
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            await SendAsync(new TokenErrorResponse("invalid_client", "Client credentials are required."), 401, ct);
            return;
        }

        if (!Guid.TryParse(serverId, out var serverIdGuid))
        {
            await SendAsync(new TokenErrorResponse("invalid_request", "server_id must be a valid GUID."), 400, ct);
            return;
        }

        var scopes = string.IsNullOrWhiteSpace(scope)
            ? null
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var result = await clientCredentialsHandler.HandleAsync(
            new ValidateTokenRequestQuery(clientId, clientSecret, serverIdGuid, scopes, authMethod), ct);

        await SendTokenResultAsync(result, ct);
    }

    private async Task HandleJwtBearerAsync(
        string? assertion, string? serverId, string? scope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(assertion))
        {
            await SendAsync(new TokenErrorResponse("invalid_request", "assertion is required for jwt-bearer grant type."), 400, ct);
            return;
        }

        if (!Guid.TryParse(serverId, out var serverIdGuid))
        {
            await SendAsync(new TokenErrorResponse("invalid_request", "server_id must be a valid GUID."), 400, ct);
            return;
        }

        var scopes = string.IsNullOrWhiteSpace(scope)
            ? null
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var result = await userTokenHandler.HandleAsync(
            new ValidateUserTokenExchangeQuery(assertion, serverIdGuid, scopes), ct);

        await SendTokenResultAsync(result, ct);
    }

    private async Task SendTokenResultAsync(TokenValidationResult result, CancellationToken ct)
    {
        if (!result.IsAuthorized)
        {
            var statusCode = result.Error is "invalid_client" or "unauthorized_client" or "invalid_grant" ? 401 : 400;
            await SendAsync(new TokenErrorResponse(result.Error!, result.ErrorDescription), statusCode, ct);
            return;
        }

        await SendOkAsync(new TokenResponse(result.AccessToken!, "Bearer", result.ExpiresIn!.Value, result.GrantedScopes!), ct);
    }
}
