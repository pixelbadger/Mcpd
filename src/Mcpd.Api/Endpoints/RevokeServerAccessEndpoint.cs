using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Commands;
using Mediator;

namespace Mcpd.Api.Endpoints;

public sealed class RevokeServerAccessRequest
{
    public Guid ServerId { get; set; }
    public string ClientId { get; set; } = string.Empty;
}

public sealed class RevokeServerAccessEndpoint(IMediator mediator)
    : Endpoint<RevokeServerAccessRequest>
{
    public override void Configure()
    {
        Delete("/admin/servers/{serverId}/grants/{clientId}");
        AllowAnonymous();
        PreProcessor<AdminApiKeyPreProcessor<RevokeServerAccessRequest>>();
        Description(x => x.WithName("RevokeServerAccess"));
    }

    public override async Task HandleAsync(RevokeServerAccessRequest req, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new RevokeServerAccessCommand(req.ServerId, req.ClientId), ct);
            await SendNoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
