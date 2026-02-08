using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Commands;
using Mediator;

namespace Mcpd.Api.Endpoints;

public sealed class RotateClientSecretEndpoint(IMediator mediator)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/admin/clients/{clientId}/rotate-secret");
        AllowAnonymous();
        PreProcessor<AdminApiKeyPreProcessor<EmptyRequest>>();
        Description(x => x.WithName("RotateClientSecret"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var clientId = Route<string>("clientId");

        try
        {
            var result = await mediator.Send(new RotateClientSecretCommand(clientId!), ct);
            await SendOkAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
