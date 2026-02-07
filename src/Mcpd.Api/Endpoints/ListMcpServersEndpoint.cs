using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Queries;

namespace Mcpd.Api.Endpoints;

public sealed class ListMcpServersRequest
{
    // Marker request for PreProcessor support
    public string? Unused { get; set; }
}

public sealed class ListMcpServersEndpoint(ListMcpServersQueryHandler handler)
    : Endpoint<ListMcpServersRequest>
{
    public override void Configure()
    {
        Get("/admin/servers");
        AllowAnonymous();
        PreProcessor<AdminApiKeyPreProcessor<ListMcpServersRequest>>();
        Description(x => x.WithName("ListMcpServers"));
    }

    public override async Task HandleAsync(ListMcpServersRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(ct);
        await SendAsync(result, cancellation: ct);
    }
}
