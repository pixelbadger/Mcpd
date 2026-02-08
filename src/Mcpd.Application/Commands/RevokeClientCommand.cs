using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Commands;

public sealed record RevokeClientCommand(string ClientId) : ICommand;

public sealed class RevokeClientCommandHandler(
    IClientRegistrationRepository clientRepo,
    IAuditLogRepository auditRepo) : ICommandHandler<RevokeClientCommand>
{
    public async ValueTask<Unit> Handle(RevokeClientCommand command, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(command.ClientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        registration.Revoke();
        await clientRepo.UpdateAsync(registration, ct);

        await auditRepo.AddAsync(new AuditLogEntry(
            "ClientRevoked", command.ClientId, registration.Id, null, "Client registration revoked"), ct);

        return Unit.Value;
    }
}
