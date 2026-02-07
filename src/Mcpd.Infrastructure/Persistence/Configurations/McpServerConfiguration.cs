using Mcpd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mcpd.Infrastructure.Persistence.Configurations;

public sealed class McpServerConfiguration : IEntityTypeConfiguration<McpServer>
{
    public void Configure(EntityTypeBuilder<McpServer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).HasMaxLength(1024);
        builder.Property(x => x.BaseUri)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => new Uri(v));

        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasMany(x => x.CallbackWhitelist)
            .WithOne(x => x.Server)
            .HasForeignKey(x => x.McpServerId);

        builder.HasMany(x => x.ClientGrants)
            .WithOne(x => x.Server)
            .HasForeignKey(x => x.McpServerId);
    }
}
