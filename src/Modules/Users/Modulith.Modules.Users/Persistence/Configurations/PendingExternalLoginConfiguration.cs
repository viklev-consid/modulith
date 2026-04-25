using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class PendingExternalLoginConfiguration : IEntityTypeConfiguration<PendingExternalLogin>
{
    public void Configure(EntityTypeBuilder<PendingExternalLogin> builder)
    {
        builder.ToTable("pending_external_logins");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new PendingExternalLoginId(v));

        builder.Property(p => p.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.Subject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(p => p.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.IsExistingUser).IsRequired();
        builder.Property(p => p.TokenHash).IsRequired();
        builder.Property(p => p.CreatedFromIp).HasMaxLength(45); // max IPv6 length
        builder.Property(p => p.UserAgent).HasMaxLength(512);
        builder.Property(p => p.IssuedAt).IsRequired();
        builder.Property(p => p.ExpiresAt).IsRequired();
        builder.Property(p => p.ConsumedAt);

        // Lookup by hashed token on confirmation.
        builder.HasIndex(p => p.TokenHash).IsUnique();

        // Coalescing: find an active pending record for (provider, subject).
        builder.HasIndex(p => new { p.Provider, p.Subject })
            .HasFilter("consumed_at IS NULL");

        // Sweep job: delete expired records.
        builder.HasIndex(p => p.ExpiresAt);
    }
}
