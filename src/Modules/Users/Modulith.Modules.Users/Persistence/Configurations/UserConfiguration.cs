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
            .HasConversion(h => h.Value, v => new PasswordHash(v))
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.CreatedBy).HasMaxLength(100);
        builder.Property(u => u.UpdatedAt);
        builder.Property(u => u.UpdatedBy).HasMaxLength(100);
    }
}
