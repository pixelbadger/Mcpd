using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Contracts;
using Mcpd.Application.Queries;

namespace Mcpd.Api.Endpoints;

public sealed class GetClientRegistrationRequest
{
    public string ClientId { get; set; } = string.Empty;
}

public sealed class GetClientRegistrationEndpoint(GetClientRegistrationQueryHandler handler)
    : Endpoint<GetClientRegistrationRequest, ClientRegistrationResponse>
{
    public override void Configure()
    {
        Get("/register/{clientId}");
        AllowAnonymous();
        PreProcessor<RegistrationAccessTokenPreProcessor<GetClientRegistrationRequest>>();
        Description(x => x.WithName("GetClientRegistration"));
    }

    public override async Task HandleAsync(GetClientRegistrationRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetClientRegistrationQuery(req.ClientId), ct);
        if (result is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendOkAsync(result, ct);
    }
}
