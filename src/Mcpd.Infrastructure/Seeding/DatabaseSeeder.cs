using Mcpd.Domain.Entities;
using Mcpd.Infrastructure.Configuration;
using Mcpd.Infrastructure.Persistence;

namespace Mcpd.Infrastructure.Seeding;

public sealed class DatabaseSeeder(McpdDbContext db)
{
    public async Task SeedAsync(IReadOnlyList<McpServerConfig> serverConfigs, CancellationToken ct = default)
    {
        foreach (var config in serverConfigs)
        {
            var existing = db.McpServers.FirstOrDefault(s => s.Name == config.Name);
            if (existing is not null) continue;

            var server = new McpServer(config.Name, config.Description, new Uri(config.BaseUri));

            foreach (var pattern in config.CallbackWhitelist)
            {
                server.AddCallbackWhitelistEntry(pattern);
            }

            await db.McpServers.AddAsync(server, ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
