using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, v => new RefreshTokenId(v));

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .IsRequired();

        builder.Property(t => t.IssuedAt).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.RevokedAt);

        builder.Property(t => t.RotatedTo)
            .HasConversion(id => id == null ? (Guid?)null : id.Value, v => v == null ? null : new RefreshTokenId(v.Value));

        builder.Property(t => t.DeviceFingerprint).HasMaxLength(64);
        builder.Property(t => t.CreatedFromIp).HasMaxLength(45); // max IPv6 length

        // Lookup by token hash (primary query path on refresh)
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Active-session queries: user's non-revoked tokens
        builder.HasIndex(t => new { t.UserId, t.RevokedAt });

        // Sweep job: expired tokens
        builder.HasIndex(t => t.ExpiresAt);
    }
}
