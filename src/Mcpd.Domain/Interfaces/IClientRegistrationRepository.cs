using Mcpd.Domain.Entities;

namespace Mcpd.Domain.Interfaces;

public interface IClientRegistrationRepository
{
    Task<ClientRegistration?> GetByClientIdAsync(string clientId, CancellationToken ct);
    Task<ClientRegistration?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(ClientRegistration registration, CancellationToken ct);
    Task UpdateAsync(ClientRegistration registration, CancellationToken ct);
}
