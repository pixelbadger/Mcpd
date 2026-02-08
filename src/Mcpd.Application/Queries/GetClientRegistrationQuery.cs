using Mcpd.Application.Contracts;
using Mcpd.Domain.Interfaces;
using Mediator;

namespace Mcpd.Application.Queries;

public sealed record GetClientRegistrationQuery(string ClientId) : IQuery<ClientRegistrationResponse?>;

public sealed class GetClientRegistrationQueryHandler(
    IClientRegistrationRepository clientRepo) : IQueryHandler<GetClientRegistrationQuery, ClientRegistrationResponse?>
{
    public async ValueTask<ClientRegistrationResponse?> Handle(GetClientRegistrationQuery query, CancellationToken ct)
    {
        var registration = await clientRepo.GetByClientIdAsync(query.ClientId, ct);
        if (registration is null) return null;

        return new ClientRegistrationResponse(
            registration.ClientId,
            null,
            registration.ClientName,
            registration.RedirectUris,
            registration.GrantTypes,
            registration.TokenEndpointAuthMethod,
            registration.Scope,
            null,
            registration.SecretExpiresAt);
    }
}
