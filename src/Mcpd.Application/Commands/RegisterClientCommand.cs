using Mcpd.Application.Contracts;
using Mcpd.Application.Interfaces;
using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Mcpd.Domain.ValueObjects;
using Mediator;

namespace Mcpd.Application.Commands;

public sealed record RegisterClientCommand(
    string ClientName,
    string[] RedirectUris,
    string[] GrantTypes,
    string TokenEndpointAuthMethod,
    Guid[] RequestedServerIds,
    Dictionary<Guid, string[]> RequestedScopes) : ICommand<ClientRegistrationResponse>;

public sealed class RegisterClientCommandHandler(
    IClientRegistrationRepository clientRepo,
    IMcpServerRepository serverRepo,
    IClientServerGrantRepository grantRepo,
    ICallbackValidator callbackValidator,
    ISecretHasher secretHasher,
    ITokenGenerator tokenGenerator,
    IAuditLogRepository auditRepo) : ICommandHandler<RegisterClientCommand, ClientRegistrationResponse>
{
    public async ValueTask<ClientRegistrationResponse> Handle(RegisterClientCommand command, CancellationToken ct)
    {
        // Validate all requested servers exist and are active
        var servers = new List<McpServer>();
        foreach (var serverId in command.RequestedServerIds)
        {
            var server = await serverRepo.GetByIdAsync(serverId, ct);
            if (server is null || !server.IsActive)
                throw new InvalidOperationException($"Server {serverId} not found or inactive.");
            servers.Add(server);
        }

        // Validate callbacks against each server's whitelist
        foreach (var server in servers)
        {
            var result = await callbackValidator.ValidateAsync(server.Id, command.RedirectUris, ct);
            if (!result.IsValid)
                throw new InvalidOperationException(
                    $"invalid_redirect_uri: {string.Join("; ", result.Errors)}");
        }

        // Generate credentials
        var clientId = tokenGenerator.GenerateClientId();
        var clientSecret = tokenGenerator.GenerateClientSecret();
        var rat = tokenGenerator.GenerateRegistrationAccessToken();

        var secretHash = secretHasher.Hash(clientSecret);
        var ratHash = secretHasher.Hash(rat);

        // Create registration
        var registration = new ClientRegistration(
            clientId,
            secretHash.Value,
            command.ClientName,
            command.TokenEndpointAuthMethod,
            command.GrantTypes,
            command.RedirectUris,
            ratHash.Value);

        registration.SetSecretExpiry(DateTimeOffset.UtcNow.AddDays(90));

        await clientRepo.AddAsync(registration, ct);

        // Create per-server grants
        var grantSummaries = new List<ServerGrantSummary>();
        foreach (var server in servers)
        {
            var scopes = command.RequestedScopes.TryGetValue(server.Id, out var s) ? s : [];
            var grant = new ClientServerGrant(registration.Id, server.Id, scopes);
            await grantRepo.AddAsync(grant, ct);
            grantSummaries.Add(new ServerGrantSummary(server.Id, server.Name, scopes, true));
        }

        // Audit
        await auditRepo.AddAsync(new AuditLogEntry(
            "ClientRegistered", clientId, registration.Id, null,
            $"Registered with access to servers: {string.Join(", ", servers.Select(s => s.Name))}"), ct);

        return new ClientRegistrationResponse(
            clientId,
            clientSecret,
            command.ClientName,
            command.RedirectUris,
            command.GrantTypes,
            command.TokenEndpointAuthMethod,
            rat,
            registration.SecretExpiresAt,
            grantSummaries.ToArray());
    }
}
