using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;

namespace Mcpd.Application.Commands;

public sealed record RevokeServerAccessCommand(Guid ServerId, string ClientId);

public sealed class RevokeServerAccessCommandHandler(
    IClientRegistrationRepository clientRepo,
    IClientServerGrantRepository grantRepo,
    IAuditLogRepository auditRepo)
{
    public async Task HandleAsync(RevokeServerAccessCommand command, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(command.ClientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        var grant = await grantRepo.GetAsync(registration.Id, command.ServerId, ct)
            ?? throw new InvalidOperationException("Grant not found.");

        grant.Revoke();
        await grantRepo.UpdateAsync(grant, ct);

        await auditRepo.AddAsync(new AuditLogEntry(
            "ServerAccessRevoked", command.ClientId, registration.Id, command.ServerId,
            "Server access revoked"), ct);
    }
}
