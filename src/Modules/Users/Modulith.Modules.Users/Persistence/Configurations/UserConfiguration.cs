using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(u => u.Email)
            .HasConversion(e => e.Value, v => new Email(v))
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasConversion(
                h => h != null ? h.Value : null,
                v => v != null ? new PasswordHash(v) : null)
            .IsRequired(false);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion(r => r.Name, v => new Role(v))
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue(Role.User);

        // xmin is a Postgres system column that advances on every row write.
        // EF Core includes it in UPDATE WHERE clauses; a mismatch throws DbUpdateConcurrencyException.
        // No migration required — xmin is always present on every Postgres table.
        // IsRowVersion() sets IsConcurrencyToken() + ValueGeneratedOnAddOrUpdate() together.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.CreatedBy).HasMaxLength(100);
        builder.Property(u => u.UpdatedAt);
        builder.Property(u => u.UpdatedBy).HasMaxLength(100);
    }
}
