using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new ExternalLoginId(v));

        builder.Property(e => e.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(e => e.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Subject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.LinkedAt).IsRequired();

        // Unique per (provider, subject) — a Google account can only be linked to one user.
        builder.HasIndex(e => new { e.Provider, e.Subject }).IsUnique();

        // Fast lookup of all providers linked to a user.
        builder.HasIndex(e => e.UserId);
    }
}
