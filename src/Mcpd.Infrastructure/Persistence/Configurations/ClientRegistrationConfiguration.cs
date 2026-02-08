using Mcpd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mcpd.Infrastructure.Persistence.Configurations;

public sealed class ClientRegistrationConfiguration : IEntityTypeConfiguration<ClientRegistration>
{
    public void Configure(EntityTypeBuilder<ClientRegistration> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ClientSecretHash).IsRequired();
        builder.Property(x => x.ClientName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.TokenEndpointAuthMethod).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RegistrationAccessToken);

        builder.Property(x => x.GrantTypes)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => v.ToArray()));

        builder.Property(x => x.RedirectUris)
            .HasConversion(
                v => string.Join('\n', v),
                v => v.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => v.ToArray()));

        builder.Property(x => x.Scope)
            .HasConversion(
                v => string.Join(' ', v),
                v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => v.ToArray()));

        builder.HasIndex(x => x.ClientId).IsUnique();
    }
}
