using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Commands;

namespace Mcpd.Api.Endpoints;

public sealed class RotateClientSecretRequest
{
    public string ClientId { get; set; } = string.Empty;
}

public sealed class RotateClientSecretEndpoint(RotateClientSecretCommandHandler handler)
    : Endpoint<RotateClientSecretRequest>
{
    public override void Configure()
    {
        Post("/admin/clients/{clientId}/rotate-secret");
        AllowAnonymous();
        PreProcessor<AdminApiKeyPreProcessor<RotateClientSecretRequest>>();
        Description(x => x.WithName("RotateClientSecret"));
    }

    public override async Task HandleAsync(RotateClientSecretRequest req, CancellationToken ct)
    {
        try
        {
            var result = await handler.HandleAsync(new RotateClientSecretCommand(req.ClientId), ct);
            await SendOkAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
