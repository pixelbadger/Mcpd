using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Commands;

namespace Mcpd.Api.Endpoints;

public sealed class RevokeClientRegistrationRequest
{
    public string ClientId { get; set; } = string.Empty;
}

public sealed class RevokeClientRegistrationEndpoint(RevokeClientCommandHandler handler)
    : Endpoint<RevokeClientRegistrationRequest>
{
    public override void Configure()
    {
        Delete("/register/{clientId}");
        AllowAnonymous();
        PreProcessor<RegistrationAccessTokenPreProcessor<RevokeClientRegistrationRequest>>();
        Description(x => x.WithName("RevokeClientRegistration"));
    }

    public override async Task HandleAsync(RevokeClientRegistrationRequest req, CancellationToken ct)
    {
        try
        {
            await handler.HandleAsync(new RevokeClientCommand(req.ClientId), ct);
            await SendNoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
