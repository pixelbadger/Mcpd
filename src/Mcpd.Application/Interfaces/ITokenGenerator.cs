namespace Mcpd.Application.Interfaces;

public interface ITokenGenerator
{
    string GenerateClientId();
    string GenerateClientSecret();
    string GenerateRegistrationAccessToken();
    string GenerateAccessToken(string clientId, Guid serverId, string serverName, string[] scopes, TimeSpan lifetime);
    string GenerateUserAccessToken(string userSubject, string? preferredUsername, Guid serverId, string serverName, string[] scopes, TimeSpan lifetime);
}
