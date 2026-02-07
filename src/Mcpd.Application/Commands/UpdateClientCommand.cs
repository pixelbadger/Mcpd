using Mcpd.Application.Contracts;
using Mcpd.Application.Interfaces;
using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;

namespace Mcpd.Application.Commands;

public sealed record UpdateClientCommand(
    string ClientId,
    string ClientName,
    string[] RedirectUris,
    string[] GrantTypes,
    string TokenEndpointAuthMethod,
    Guid[]? AdditionalServerIds,
    Dictionary<Guid, string[]>? AdditionalScopes);

public sealed class UpdateClientCommandHandler(
    IClientRegistrationRepository clientRepo,
    IMcpServerRepository serverRepo,
    IClientServerGrantRepository grantRepo,
    ICallbackValidator callbackValidator,
    IAuditLogRepository auditRepo)
{
    public async Task<ClientRegistrationResponse> HandleAsync(UpdateClientCommand command, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(command.ClientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        // Validate callbacks against all currently-granted servers
        var existingGrants = await grantRepo.GetGrantsForClientAsync(registration.Id, ct);
        var serverIds = existingGrants.Where(g => g.IsActive).Select(g => g.McpServerId).ToList();

        if (command.AdditionalServerIds is { Length: > 0 })
        {
            foreach (var sid in command.AdditionalServerIds)
            {
                if (!serverIds.Contains(sid))
                    serverIds.Add(sid);
            }
        }

        foreach (var serverId in serverIds)
        {
            var result = await callbackValidator.ValidateAsync(serverId, command.RedirectUris, ct);
            if (!result.IsValid)
                throw new InvalidOperationException(
                    $"invalid_redirect_uri: {string.Join("; ", result.Errors)}");
        }

        registration.UpdateMetadata(command.ClientName, command.RedirectUris, command.TokenEndpointAuthMethod, command.GrantTypes);
        await clientRepo.UpdateAsync(registration, ct);

        // Create grants for additional servers
        if (command.AdditionalServerIds is { Length: > 0 })
        {
            foreach (var serverId in command.AdditionalServerIds)
            {
                var existing = await grantRepo.GetAsync(registration.Id, serverId, ct);
                if (existing is not null) continue;

                var server = await serverRepo.GetByIdAsync(serverId, ct)
                    ?? throw new InvalidOperationException($"Server {serverId} not found.");

                var scopes = command.AdditionalScopes?.TryGetValue(serverId, out var s) == true ? s : [];
                var grant = new ClientServerGrant(registration.Id, serverId, scopes);
                await grantRepo.AddAsync(grant, ct);
            }
        }

        await auditRepo.AddAsync(new AuditLogEntry(
            "ClientUpdated", command.ClientId, registration.Id, null, "Client metadata updated"), ct);

        var allGrants = await grantRepo.GetGrantsForClientAsync(registration.Id, ct);
        var summaries = new List<ServerGrantSummary>();
        foreach (var g in allGrants)
        {
            var server = await serverRepo.GetByIdAsync(g.McpServerId, ct);
            summaries.Add(new ServerGrantSummary(g.McpServerId, server?.Name ?? "unknown", g.Scopes, g.IsActive));
        }

        return new ClientRegistrationResponse(
            registration.ClientId,
            null,
            registration.ClientName,
            registration.RedirectUris,
            registration.GrantTypes,
            registration.TokenEndpointAuthMethod,
            null,
            registration.SecretExpiresAt,
            summaries.ToArray());
    }
}
