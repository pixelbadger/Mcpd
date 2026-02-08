using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Commands;

namespace Mcpd.Api.Endpoints;

public sealed class RotateClientSecretEndpoint(RotateClientSecretCommandHandler handler)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/admin/clients/{clientId}/rotate-secret");
        AllowAnonymous();
        PreProcessor<AdminAuthPreProcessor<EmptyRequest>>();
        Description(x => x.WithName("RotateClientSecret"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var clientId = Route<string>("clientId");

        try
        {
            var result = await handler.HandleAsync(new RotateClientSecretCommand(clientId!), ct);
            await SendOkAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
