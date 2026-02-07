using Mcpd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mcpd.Infrastructure.Persistence.Configurations;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ActorId).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Detail).HasMaxLength(4096);

        builder.HasIndex(x => x.Timestamp);
    }
}
