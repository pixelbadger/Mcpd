using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Mcpd.Infrastructure.Persistence.Repositories;

public sealed class ClientServerGrantRepository(McpdDbContext db) : IClientServerGrantRepository
{
    public async Task<ClientServerGrant?> GetAsync(Guid clientId, Guid serverId, CancellationToken ct) =>
        await db.ClientServerGrants
            .FirstOrDefaultAsync(x => x.ClientRegistrationId == clientId && x.McpServerId == serverId, ct);

    public async Task<IReadOnlyList<ClientServerGrant>> GetGrantsForClientAsync(Guid clientId, CancellationToken ct) =>
        await db.ClientServerGrants
            .Where(x => x.ClientRegistrationId == clientId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ClientServerGrant>> GetGrantsForServerAsync(Guid serverId, CancellationToken ct) =>
        await db.ClientServerGrants
            .Where(x => x.McpServerId == serverId)
            .ToListAsync(ct);

    public async Task AddAsync(ClientServerGrant grant, CancellationToken ct)
    {
        await db.ClientServerGrants.AddAsync(grant, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ClientServerGrant grant, CancellationToken ct)
    {
        db.ClientServerGrants.Update(grant);
        await db.SaveChangesAsync(ct);
    }
}
