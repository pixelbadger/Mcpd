using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Mcpd.Infrastructure.Persistence.Repositories;

public sealed class McpServerRepository(McpdDbContext db) : IMcpServerRepository
{
    public async Task<McpServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await db.McpServers
            .Include(x => x.CallbackWhitelist)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<McpServer?> GetByNameAsync(string name, CancellationToken ct) =>
        await db.McpServers
            .Include(x => x.CallbackWhitelist)
            .FirstOrDefaultAsync(x => x.Name == name, ct);

    public async Task<IReadOnlyList<McpServer>> GetAllActiveAsync(CancellationToken ct) =>
        await db.McpServers
            .Where(x => x.IsActive)
            .ToListAsync(ct);
}
