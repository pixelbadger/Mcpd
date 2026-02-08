using Mcpd.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mcpd.Infrastructure.Persistence;

public sealed class McpdDbContext(DbContextOptions<McpdDbContext> options) : DbContext(options)
{
    public DbSet<ClientRegistration> ClientRegistrations => Set<ClientRegistration>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(McpdDbContext).Assembly);
    }
}
