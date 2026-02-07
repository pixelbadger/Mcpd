using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;

namespace Mcpd.Application.Queries;

public sealed record ListMcpServersQuery;

public sealed record McpServerSummary(Guid Id, string Name, string Description, Uri BaseUri, bool IsActive);

public sealed class ListMcpServersQueryHandler(IMcpServerRepository serverRepo)
{
    public async Task<IReadOnlyList<McpServerSummary>> HandleAsync(CancellationToken ct)
    {
        var servers = await serverRepo.GetAllActiveAsync(ct);
        return servers.Select(s => new McpServerSummary(s.Id, s.Name, s.Description, s.BaseUri, s.IsActive)).ToList();
    }
}
