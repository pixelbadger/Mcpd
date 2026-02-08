using System.Text;
using FastEndpoints;
using Mcpd.Application.Contracts;
using Mcpd.Application.Queries;
using Mediator;

namespace Mcpd.Api.Endpoints;

public sealed class TokenEndpoint(IMediator mediator)
    : EndpointWithoutRequest
{
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
        string? audience = null;
        string? scope = null;

        if (HttpContext.Request.HasFormContentType)
        {
            var form = await HttpContext.Request.ReadFormAsync(ct);
            grantType = form["grant_type"].ToString();
            clientId = form["client_id"].ToString();
            clientSecret = form["client_secret"].ToString();
            // Accept both "resource" (RFC 8707) and "audience" as the audience parameter
            audience = form["resource"].ToString();
            if (string.IsNullOrWhiteSpace(audience))
                audience = form["audience"].ToString();
            var scopeValues = form["scope"];
            scope = scopeValues.Count > 1
                ? string.Join(" ", scopeValues!)
                : scopeValues.ToString();
        }

        if (string.IsNullOrWhiteSpace(grantType) || grantType != "client_credentials")
        {
            await SendAsync(new TokenErrorResponse("unsupported_grant_type", "Only client_credentials is supported."), 400, ct);
            return;
        }

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

        var scopes = string.IsNullOrWhiteSpace(scope)
            ? null
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var effectiveAudience = string.IsNullOrWhiteSpace(audience) ? null : audience;

        var result = await mediator.Send(
            new ValidateTokenRequestQuery(clientId, clientSecret, scopes, authMethod, effectiveAudience), ct);

        if (!result.IsAuthorized)
        {
            var statusCode = result.Error == "invalid_client" || result.Error == "unauthorized_client" ? 401 : 400;
            await SendAsync(new TokenErrorResponse(result.Error!, result.ErrorDescription), statusCode, ct);
            return;
        }

        await SendOkAsync(new TokenResponse(result.AccessToken!, "Bearer", result.ExpiresIn!.Value, result.GrantedScopes!), ct);
    }
}
