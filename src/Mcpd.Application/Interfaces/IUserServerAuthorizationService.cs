namespace Mcpd.Application.Interfaces;

public interface IUserServerAuthorizationService
{
    UserServerAuthorizationResult Authorize(string serverName, string[] userClaims, string[]? requestedScopes);
    bool IsAdmin(string[] userClaims);
}

public sealed record UserServerAuthorizationResult(
    bool IsAuthorized,
    string[] GrantedScopes,
    string? Error);
