using Mcpd.Domain.Entities;

namespace Mcpd.Domain.Interfaces;

public interface IMcpServerRepository
{
    Task<McpServer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<McpServer?> GetByNameAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<McpServer>> GetAllActiveAsync(CancellationToken ct);
}
