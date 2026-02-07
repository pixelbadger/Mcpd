using Mcpd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mcpd.Infrastructure.Persistence.Configurations;

public sealed class ClientServerGrantConfiguration : IEntityTypeConfiguration<ClientServerGrant>
{
    public void Configure(EntityTypeBuilder<ClientServerGrant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Scopes)
            .HasConversion(
                v => string.Join(' ', v),
                v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => v.ToArray()));

        builder.HasIndex(x => new { x.ClientRegistrationId, x.McpServerId }).IsUnique();
    }
}
