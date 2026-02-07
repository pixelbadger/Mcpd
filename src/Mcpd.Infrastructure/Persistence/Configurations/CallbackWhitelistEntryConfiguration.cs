using Mcpd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mcpd.Infrastructure.Persistence.Configurations;

public sealed class CallbackWhitelistEntryConfiguration : IEntityTypeConfiguration<CallbackWhitelistEntry>
{
    public void Configure(EntityTypeBuilder<CallbackWhitelistEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Pattern).IsRequired().HasMaxLength(2048);

        builder.HasOne(x => x.Server)
            .WithMany(x => x.CallbackWhitelist)
            .HasForeignKey(x => x.McpServerId);
    }
}
