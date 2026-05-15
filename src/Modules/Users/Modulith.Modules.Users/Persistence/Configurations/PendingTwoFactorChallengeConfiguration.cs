using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class PendingTwoFactorChallengeConfiguration : IEntityTypeConfiguration<PendingTwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<PendingTwoFactorChallenge> builder)
    {
        builder.ToTable("pending_two_factor_challenges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, v => new PendingTwoFactorChallengeId(v));

        builder.Property(c => c.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(c => c.TokenHash)
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => c.UserId);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ExpiresAt).IsRequired();
        builder.Property(c => c.ConsumedAt);
        builder.Property(c => c.AttemptCount).IsRequired();
        builder.Property(c => c.IpAddress).HasMaxLength(64);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.UpdatedBy).HasMaxLength(100);
    }
}
