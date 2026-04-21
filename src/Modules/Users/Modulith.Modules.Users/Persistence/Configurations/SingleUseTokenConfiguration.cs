using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class SingleUseTokenConfiguration : IEntityTypeConfiguration<SingleUseToken>
{
    public void Configure(EntityTypeBuilder<SingleUseToken> builder)
    {
        builder.ToTable("user_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, v => new SingleUseTokenId(v));

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(t => t.TokenHash).IsRequired();

        builder.Property(t => t.Purpose).IsRequired();

        builder.Property(t => t.IssuedAt).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.ConsumedAt);

        // Lookup by hash + purpose (prevents cross-purpose token reuse)
        builder.HasIndex(t => new { t.TokenHash, t.Purpose }).IsUnique();

        // Sweep job
        builder.HasIndex(t => t.ExpiresAt);
    }
}
