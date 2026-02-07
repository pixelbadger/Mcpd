using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;

namespace Mcpd.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(McpdDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct)
    {
        await db.AuditLog.AddAsync(entry, ct);
        await db.SaveChangesAsync(ct);
    }
}
