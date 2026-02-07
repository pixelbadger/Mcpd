using FastEndpoints;
using Mcpd.Api.PreProcessors;
using Mcpd.Application.Commands;
using Mcpd.Application.Contracts;

namespace Mcpd.Api.Endpoints;

public sealed class UpdateClientRegistrationRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string[] RedirectUris { get; set; } = [];
    public string[] GrantTypes { get; set; } = ["client_credentials"];
    public string TokenEndpointAuthMethod { get; set; } = "client_secret_post";
    public Guid[]? AdditionalServerIds { get; set; }
    public Dictionary<Guid, string[]>? AdditionalScopes { get; set; }
}

public sealed class UpdateClientRegistrationEndpoint(UpdateClientCommandHandler handler)
    : Endpoint<UpdateClientRegistrationRequest, ClientRegistrationResponse>
{
    public override void Configure()
    {
        Put("/register/{clientId}");
        AllowAnonymous();
        PreProcessor<RegistrationAccessTokenPreProcessor<UpdateClientRegistrationRequest>>();
        Description(x => x.WithName("UpdateClientRegistration"));
    }

    public override async Task HandleAsync(UpdateClientRegistrationRequest req, CancellationToken ct)
    {
        var command = new UpdateClientCommand(
            req.ClientId,
            req.ClientName,
            req.RedirectUris,
            req.GrantTypes,
            req.TokenEndpointAuthMethod,
            req.AdditionalServerIds,
            req.AdditionalScopes);

        try
        {
            var response = await handler.HandleAsync(command, ct);
            await SendOkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
