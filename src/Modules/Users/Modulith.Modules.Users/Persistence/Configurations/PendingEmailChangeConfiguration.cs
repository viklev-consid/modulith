using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class PendingEmailChangeConfiguration : IEntityTypeConfiguration<PendingEmailChange>
{
    public void Configure(EntityTypeBuilder<PendingEmailChange> builder)
    {
        builder.ToTable("pending_email_changes");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new PendingEmailChangeId(v));

        builder.Property(p => p.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(p => p.NewEmail)
            .HasConversion(e => e.Value, v => new Email(v))
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(p => p.TokenId)
            .HasConversion(id => id.Value, v => new SingleUseTokenId(v))
            .IsRequired();

        // One pending change per user maximum (enforced at application level; unique index ensures it)
        builder.HasIndex(p => p.UserId).IsUnique();
    }
}
