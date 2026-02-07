using Mcpd.Domain.Entities;

namespace Mcpd.Domain.Interfaces;

public interface IClientServerGrantRepository
{
    Task<ClientServerGrant?> GetAsync(Guid clientId, Guid serverId, CancellationToken ct);
    Task<IReadOnlyList<ClientServerGrant>> GetGrantsForClientAsync(Guid clientId, CancellationToken ct);
    Task<IReadOnlyList<ClientServerGrant>> GetGrantsForServerAsync(Guid serverId, CancellationToken ct);
    Task AddAsync(ClientServerGrant grant, CancellationToken ct);
    Task UpdateAsync(ClientServerGrant grant, CancellationToken ct);
}
