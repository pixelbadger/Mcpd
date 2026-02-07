using Mcpd.Domain.Entities;
using Mcpd.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Mcpd.Infrastructure.Persistence.Repositories;

public sealed class ClientRegistrationRepository(McpdDbContext db) : IClientRegistrationRepository
{
    public async Task<ClientRegistration?> GetByClientIdAsync(string clientId, CancellationToken ct) =>
        await db.ClientRegistrations
            .Include(x => x.ServerGrants)
            .FirstOrDefaultAsync(x => x.ClientId == clientId, ct);

    public async Task<ClientRegistration?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await db.ClientRegistrations
            .Include(x => x.ServerGrants)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(ClientRegistration registration, CancellationToken ct)
    {
        await db.ClientRegistrations.AddAsync(registration, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ClientRegistration registration, CancellationToken ct)
    {
        db.ClientRegistrations.Update(registration);
        await db.SaveChangesAsync(ct);
    }
}
