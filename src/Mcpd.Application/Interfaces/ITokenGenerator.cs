namespace Mcpd.Application.Interfaces;

public interface ITokenGenerator
{
    string GenerateClientId();
    string GenerateClientSecret();
    string GenerateRegistrationAccessToken();
    string GenerateAccessToken(string clientId, Guid serverId, string serverName, string[] scopes, TimeSpan lifetime);
}
