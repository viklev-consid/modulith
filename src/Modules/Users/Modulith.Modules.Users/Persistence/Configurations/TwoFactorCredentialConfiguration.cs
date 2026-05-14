using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class TwoFactorCredentialConfiguration : IEntityTypeConfiguration<TwoFactorCredential>
{
    public void Configure(EntityTypeBuilder<TwoFactorCredential> builder)
    {
        builder.ToTable("two_factor_credentials");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, v => new TwoFactorCredentialId(v));

        builder.Property(c => c.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(c => c.Method)
            .HasConversion(m => m.ToString(), v => Enum.Parse<TwoFactorMethod>(v))
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.ProtectedSecret)
            .HasMaxLength(2048)
            .IsRequired();

        builder.HasIndex(c => new { c.UserId, c.Method }).IsUnique();

        builder.Property(c => c.ConfirmedAt);
        builder.Property(c => c.DisabledAt);
        builder.Property(c => c.LastAcceptedTimeStep);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.UpdatedBy).HasMaxLength(100);
    }
}
