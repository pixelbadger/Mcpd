using Mcpd.Domain.Entities;

namespace Mcpd.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct);
}
