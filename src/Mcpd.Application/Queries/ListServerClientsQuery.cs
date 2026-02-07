using Mcpd.Application.Contracts;
using Mcpd.Domain.Interfaces;

namespace Mcpd.Application.Queries;

public sealed record ListServerClientsQuery(Guid ServerId);

public sealed record ServerClientSummary(string ClientId, string ClientName, string[] Scopes, bool IsActive);

public sealed class ListServerClientsQueryHandler(
    IClientServerGrantRepository grantRepo,
    IClientRegistrationRepository clientRepo)
{
    public async Task<IReadOnlyList<ServerClientSummary>> HandleAsync(ListServerClientsQuery query, CancellationToken ct)
    {
        var grants = await grantRepo.GetGrantsForServerAsync(query.ServerId, ct);
        var results = new List<ServerClientSummary>();

        foreach (var grant in grants.Where(g => g.IsActive))
        {
            var client = await clientRepo.GetByIdAsync(grant.ClientRegistrationId, ct);
            if (client is not null)
            {
                results.Add(new ServerClientSummary(client.ClientId, client.ClientName, grant.Scopes, grant.IsActive));
            }
        }

        return results;
    }
}
