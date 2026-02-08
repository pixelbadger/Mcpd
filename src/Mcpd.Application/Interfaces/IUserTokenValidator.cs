namespace Mcpd.Application.Interfaces;

public interface IUserTokenValidator
{
    Task<UserTokenValidationResult> ValidateAsync(string token, CancellationToken ct);
}

public sealed record UserTokenValidationResult(
    bool IsValid,
    string? Subject,
    string? PreferredUsername,
    string[] Claims,
    string? Error);
