using Mcpd.Application.Contracts;
using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Commands;

public sealed record UpdateClientCommand(
    string ClientId,
    string ClientName,
    string[] RedirectUris,
    string[] GrantTypes,
    string TokenEndpointAuthMethod,
    string[] Scope) : ICommand<ClientRegistrationResponse>;

public sealed class UpdateClientCommandHandler(
    IClientRegistrationRepository clientRepo,
    IAuditLogRepository auditRepo) : ICommandHandler<UpdateClientCommand, ClientRegistrationResponse>
{
    public async ValueTask<ClientRegistrationResponse> Handle(UpdateClientCommand command, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(command.ClientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        registration.UpdateMetadata(command.ClientName, command.RedirectUris, command.TokenEndpointAuthMethod, command.GrantTypes, command.Scope);
        await clientRepo.UpdateAsync(registration, ct);

        await auditRepo.AddAsync(new AuditLogEntry(
            "ClientUpdated", command.ClientId, registration.Id, null, "Client metadata updated"), ct);

        return new ClientRegistrationResponse(
            registration.ClientId,
            null,
            registration.ClientName,
            registration.RedirectUris,
            registration.GrantTypes,
            registration.TokenEndpointAuthMethod,
            registration.Scope,
            null,
            registration.SecretExpiresAt);
    }
}
