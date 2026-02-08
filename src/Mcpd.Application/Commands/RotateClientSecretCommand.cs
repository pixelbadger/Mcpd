using Mcpd.Application.Interfaces;
using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Commands;

public sealed record RotateClientSecretCommand(string ClientId) : ICommand<RotateClientSecretResponse>;

public sealed record RotateClientSecretResponse(string ClientSecret, DateTimeOffset? ClientSecretExpiresAt);

public sealed class RotateClientSecretCommandHandler(
    IClientRegistrationRepository clientRepo,
    ISecretHasher secretHasher,
    ITokenGenerator tokenGenerator,
    IAuditLogRepository auditRepo) : ICommandHandler<RotateClientSecretCommand, RotateClientSecretResponse>
{
    public async ValueTask<RotateClientSecretResponse> Handle(RotateClientSecretCommand command, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(command.ClientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        var newSecret = tokenGenerator.GenerateClientSecret();
        var hash = secretHasher.Hash(newSecret);
        var newExpiry = DateTimeOffset.UtcNow.AddDays(90);

        registration.RotateSecret(hash.Value, newExpiry);
        await clientRepo.UpdateAsync(registration, ct);

        await auditRepo.AddAsync(new AuditLogEntry(
            "SecretRotated", command.ClientId, registration.Id, null, "Client secret rotated"), ct);

        return new RotateClientSecretResponse(newSecret, newExpiry);
    }
}
