namespace Mcpd.Application.Components;

public interface ITokenGenerator
{
    string GenerateClientId();
    string GenerateClientSecret();
    string GenerateRegistrationAccessToken();
    string GenerateAccessToken(string clientId, string[] scopes, TimeSpan lifetime, string? audience);
}
