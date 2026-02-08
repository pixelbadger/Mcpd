using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Commands;
using Mcpd.Application.Contracts;
using Mediator;

namespace Mcpd.Api.Endpoints;

public sealed class GrantServerAccessRequest
{
    public Guid ServerId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
}

public sealed class GrantServerAccessEndpoint(IMediator mediator)
    : Endpoint<GrantServerAccessRequest, ServerGrantSummary>
{
    public override void Configure()
    {
        Post("/admin/servers/{serverId}/grants");
        AllowAnonymous();
        PreProcessor<AdminApiKeyPreProcessor<GrantServerAccessRequest>>();
        Description(x => x.WithName("GrantServerAccess"));
    }

    public override async Task HandleAsync(GrantServerAccessRequest req, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new GrantServerAccessCommand(req.ServerId, req.ClientId, req.Scopes), ct);
            await SendAsync(result, 201, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
