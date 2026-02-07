using Mcpd.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mcpd.Infrastructure.Persistence;

public sealed class McpdDbContext(DbContextOptions<McpdDbContext> options) : DbContext(options)
{
    public DbSet<McpServer> McpServers => Set<McpServer>();
    public DbSet<ClientRegistration> ClientRegistrations => Set<ClientRegistration>();
    public DbSet<ClientServerGrant> ClientServerGrants => Set<ClientServerGrant>();
    public DbSet<CallbackWhitelistEntry> CallbackWhitelist => Set<CallbackWhitelistEntry>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(McpdDbContext).Assembly);
    }
}
