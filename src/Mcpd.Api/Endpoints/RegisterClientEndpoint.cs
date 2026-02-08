using FastEndpoints;
using Mcpd.Application.Commands;
using Mcpd.Application.Contracts;
using Mcpd.Application.Validators;
using Mediator;

namespace Mcpd.Api.Endpoints;

public sealed class RegisterClientRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string[] RedirectUris { get; set; } = [];
    public string[] GrantTypes { get; set; } = ["client_credentials"];
    public string TokenEndpointAuthMethod { get; set; } = "client_secret_post";
    public string[] Scope { get; set; } = [];
}

public sealed class RegisterClientEndpoint(IMediator mediator) : Endpoint<RegisterClientRequest, ClientRegistrationResponse>
{
    public override void Configure()
    {
        Post("/register");
        AllowAnonymous();
        Description(x => x.WithName("RegisterClient"));
    }

    public override async Task HandleAsync(RegisterClientRequest req, CancellationToken ct)
    {
        var command = new RegisterClientCommand(
            req.ClientName,
            req.RedirectUris,
            req.GrantTypes,
            req.TokenEndpointAuthMethod,
            req.Scope);

        var validator = new RegisterClientCommandValidator();
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
                AddError(error.ErrorMessage);
            await SendErrorsAsync(400, ct);
            return;
        }

        try
        {
            var response = await mediator.Send(command, ct);
            await SendAsync(response, 201, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
