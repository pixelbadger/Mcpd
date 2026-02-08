using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Queries;

public sealed record ListMcpServersQuery : IQuery<IReadOnlyList<McpServerSummary>>;

public sealed record McpServerSummary(Guid Id, string Name, string Description, Uri BaseUri, bool IsActive);

public sealed class ListMcpServersQueryHandler(IMcpServerRepository serverRepo)
    : IQueryHandler<ListMcpServersQuery, IReadOnlyList<McpServerSummary>>
{
    public async ValueTask<IReadOnlyList<McpServerSummary>> Handle(ListMcpServersQuery query, CancellationToken ct)
    {
        var servers = await serverRepo.GetAllActiveAsync(ct);
        return servers.Select(s => new McpServerSummary(s.Id, s.Name, s.Description, s.BaseUri, s.IsActive)).ToList();
    }
}
