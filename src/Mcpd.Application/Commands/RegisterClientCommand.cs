using Mcpd.Application.Contracts;
using Mcpd.Application.Interfaces;
using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Commands;

public sealed record RegisterClientCommand(
    string ClientName,
    string[] RedirectUris,
    string[] GrantTypes,
    string TokenEndpointAuthMethod,
    string[] Scope) : ICommand<ClientRegistrationResponse>;

public sealed class RegisterClientCommandHandler(
    IClientRegistrationRepository clientRepo,
    ISecretHasher secretHasher,
    ITokenGenerator tokenGenerator,
    IAuditLogRepository auditRepo) : ICommandHandler<RegisterClientCommand, ClientRegistrationResponse>
{
    public async ValueTask<ClientRegistrationResponse> Handle(RegisterClientCommand command, CancellationToken ct)
    {
        var clientId = tokenGenerator.GenerateClientId();
        var clientSecret = tokenGenerator.GenerateClientSecret();
        var rat = tokenGenerator.GenerateRegistrationAccessToken();

        var secretHash = secretHasher.Hash(clientSecret);
        var ratHash = secretHasher.Hash(rat);

        var registration = new ClientRegistration(
            clientId,
            secretHash.Value,
            command.ClientName,
            command.TokenEndpointAuthMethod,
            command.GrantTypes,
            command.RedirectUris,
            ratHash.Value,
            command.Scope);

        registration.SetSecretExpiry(DateTimeOffset.UtcNow.AddDays(90));

        await clientRepo.AddAsync(registration, ct);

        await auditRepo.AddAsync(new AuditLogEntry(
            "ClientRegistered", clientId, registration.Id, null,
            $"Client '{command.ClientName}' registered"), ct);

        return new ClientRegistrationResponse(
            clientId,
            clientSecret,
            command.ClientName,
            command.RedirectUris,
            command.GrantTypes,
            command.TokenEndpointAuthMethod,
            command.Scope,
            rat,
            registration.SecretExpiresAt);
    }
}
