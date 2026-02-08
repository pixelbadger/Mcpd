using Mcpd.Application.Contracts;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Queries;

public sealed record GetClientRegistrationQuery(string ClientId) : IQuery<ClientRegistrationResponse?>;

public sealed class GetClientRegistrationQueryHandler(
    IClientRegistrationRepository clientRepo,
    IClientServerGrantRepository grantRepo,
    IMcpServerRepository serverRepo) : IQueryHandler<GetClientRegistrationQuery, ClientRegistrationResponse?>
{
    public async ValueTask<ClientRegistrationResponse?> Handle(GetClientRegistrationQuery query, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(query.ClientId, ct);
        if (registration is null) return null;

        var grants = await grantRepo.GetGrantsForClientAsync(registration.Id, ct);
        var summaries = new List<ServerGrantSummary>();
        foreach (var g in grants)
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
