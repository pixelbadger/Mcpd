using Mcpd.Application.Contracts;
using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Commands;

public sealed record GrantServerAccessCommand(Guid ServerId, string ClientId, string[] Scopes) : ICommand<ServerGrantSummary>;

public sealed class GrantServerAccessCommandHandler(
    IClientRegistrationRepository clientRepo,
    IMcpServerRepository serverRepo,
    IClientServerGrantRepository grantRepo,
    IAuditLogRepository auditRepo) : ICommandHandler<GrantServerAccessCommand, ServerGrantSummary>
{
    public async ValueTask<ServerGrantSummary> Handle(GrantServerAccessCommand command, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(command.ClientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        var server = await serverRepo.GetByIdAsync(command.ServerId, ct)
            ?? throw new InvalidOperationException("Server not found.");

        var existing = await grantRepo.GetAsync(registration.Id, command.ServerId, ct);
        if (existing is not null && existing.IsActive)
            throw new InvalidOperationException("Grant already exists.");

        var grant = new ClientServerGrant(registration.Id, command.ServerId, command.Scopes);
        await grantRepo.AddAsync(grant, ct);

        await auditRepo.AddAsync(new AuditLogEntry(
            "ServerAccessGranted", command.ClientId, registration.Id, command.ServerId,
            $"Granted access to server {server.Name}"), ct);

        return new ServerGrantSummary(server.Id, server.Name, command.Scopes, true);
    }
}
