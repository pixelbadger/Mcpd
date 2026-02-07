namespace Mcpd.Application.Interfaces;

public interface ICallbackValidator
{
    Task<CallbackValidationResult> ValidateAsync(Guid serverId, string[] redirectUris, CancellationToken ct);
}

public sealed record CallbackValidationResult(bool IsValid, string[] Errors);
