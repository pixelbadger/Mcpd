using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Mcpd.Infrastructure.Persistence.Repositories;

public sealed class CallbackWhitelistRepository(McpdDbContext db) : ICallbackWhitelistRepository
{
    public async Task<IReadOnlyList<CallbackWhitelistEntry>> GetForServerAsync(Guid serverId, CancellationToken ct) =>
        await db.CallbackWhitelist
            .Where(x => x.McpServerId == serverId && x.IsActive)
            .ToListAsync(ct);
}
