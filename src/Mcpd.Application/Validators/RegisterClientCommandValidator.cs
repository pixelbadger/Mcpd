using FluentValidation;
using Mcpd.Application.Commands;

namespace Mcpd.Application.Validators;

public sealed class RegisterClientCommandValidator : AbstractValidator<RegisterClientCommand>
{
    private static readonly string[] AllowedGrantTypes = ["client_credentials", "authorization_code"];
    private static readonly string[] AllowedAuthMethods = ["client_secret_post", "client_secret_basic"];

    public RegisterClientCommandValidator()
    {
        RuleFor(x => x.ClientName)
            .NotEmpty().WithMessage("client_name is required.")
            .MaximumLength(256).WithMessage("client_name must not exceed 256 characters.");

        RuleFor(x => x.RequestedServerIds)
            .NotEmpty().WithMessage("At least one server_id is required.");

        RuleFor(x => x.RedirectUris)
            .NotEmpty().WithMessage("At least one redirect_uri is required.");

        RuleForEach(x => x.RedirectUris)
            .Must(BeValidAbsoluteUri).WithMessage("Each redirect_uri must be a valid absolute URI.")
            .Must(NotContainFragment).WithMessage("redirect_uri must not contain a fragment component.");

        RuleFor(x => x.GrantTypes)
            .NotEmpty().WithMessage("grant_types is required.")
            .Must(types => types.All(t => AllowedGrantTypes.Contains(t)))
            .WithMessage($"grant_types must be a subset of [{string.Join(", ", AllowedGrantTypes)}].");

        RuleFor(x => x.TokenEndpointAuthMethod)
            .NotEmpty().WithMessage("token_endpoint_auth_method is required.")
            .Must(m => AllowedAuthMethods.Contains(m))
            .WithMessage($"token_endpoint_auth_method must be one of [{string.Join(", ", AllowedAuthMethods)}].");
    }

    private static bool BeValidAbsoluteUri(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
        (parsed.Scheme == "https" || (parsed.Scheme == "http" && parsed.Host == "localhost"));

    private static bool NotContainFragment(string uri) =>
        !uri.Contains('#');
}
