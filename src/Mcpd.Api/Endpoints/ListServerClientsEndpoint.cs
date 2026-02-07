using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Queries;

namespace Mcpd.Api.Endpoints;

public sealed class ListServerClientsRequest
{
    public Guid ServerId { get; set; }
}

public sealed class ListServerClientsEndpoint(ListServerClientsQueryHandler handler)
    : Endpoint<ListServerClientsRequest>
{
    public override void Configure()
    {
        Get("/admin/servers/{serverId}/clients");
        AllowAnonymous();
        PreProcessor<AdminApiKeyPreProcessor<ListServerClientsRequest>>();
        Description(x => x.WithName("ListServerClients"));
    }

    public override async Task HandleAsync(ListServerClientsRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListServerClientsQuery(req.ServerId), ct);
        await SendAsync(result, cancellation: ct);
    }
}
