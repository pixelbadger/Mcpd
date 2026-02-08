using Mcpd.Application.Interfaces;
using Mcpd.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Mcpd.Infrastructure.Services;

public sealed class UserServerAuthorizationService(IOptions<AuthServerOptions> options)
    : IUserServerAuthorizationService
{
    private readonly AuthServerOptions _options = options.Value;

    public UserServerAuthorizationResult Authorize(string serverName, string[] userClaims, string[]? requestedScopes)
    {
        if (!_options.ServerMappings.TryGetValue(serverName, out var mapping))
            return new UserServerAuthorizationResult(false, [],
                $"No claim mapping configured for server '{serverName}'.");

        var hasAccess = mapping.RequiredRoles.Length == 0
            || mapping.RequiredRoles.Any(role => userClaims.Contains(role, StringComparer.OrdinalIgnoreCase));

        if (!hasAccess)
            return new UserServerAuthorizationResult(false, [],
                "User does not have the required role for this server.");

        var effectiveScopes = requestedScopes is { Length: > 0 }
            ? requestedScopes.Where(s => mapping.DefaultScopes.Contains(s, StringComparer.OrdinalIgnoreCase)).ToArray()
            : mapping.DefaultScopes;

        if (requestedScopes is { Length: > 0 } && effectiveScopes.Length < requestedScopes.Length)
            return new UserServerAuthorizationResult(false, [],
                "Requested scopes exceed allowed scopes for this server.");

        return new UserServerAuthorizationResult(true, effectiveScopes, null);
    }

    public bool IsAdmin(string[] userClaims)
    {
        return !string.IsNullOrWhiteSpace(_options.AdminRole)
            && userClaims.Contains(_options.AdminRole, StringComparer.OrdinalIgnoreCase);
    }
}
