using Mcpd.Domain.Entities;

namespace Mcpd.Domain.Interfaces;

public interface ICallbackWhitelistRepository
{
    Task<IReadOnlyList<CallbackWhitelistEntry>> GetForServerAsync(Guid serverId, CancellationToken ct);
}
