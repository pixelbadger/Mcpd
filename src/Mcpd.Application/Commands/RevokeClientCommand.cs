using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Commands;

public sealed record RevokeClientCommand(string ClientId) : ICommand;

public sealed class RevokeClientCommandHandler(
    IClientRegistrationRepository clientRepo,
    IClientServerGrantRepository grantRepo,
    IAuditLogRepository auditRepo) : ICommandHandler<RevokeClientCommand>
{
    public async ValueTask<Unit> Handle(RevokeClientCommand command, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(command.ClientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        registration.Revoke();

        var grants = await grantRepo.GetGrantsForClientAsync(registration.Id, ct);
        foreach (var grant in grants.Where(g => g.IsActive))
        {
            grant.Revoke();
            await grantRepo.UpdateAsync(grant, ct);
        }

        await clientRepo.UpdateAsync(registration, ct);

        await auditRepo.AddAsync(new AuditLogEntry(
            "ClientRevoked", command.ClientId, registration.Id, null, "Client registration revoked"), ct);

        return Unit.Value;
    }
}
