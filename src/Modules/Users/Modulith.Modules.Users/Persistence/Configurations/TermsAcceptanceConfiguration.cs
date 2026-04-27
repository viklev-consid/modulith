using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class TermsAcceptanceConfiguration : IEntityTypeConfiguration<TermsAcceptance>
{
    public void Configure(EntityTypeBuilder<TermsAcceptance> builder)
    {
        builder.ToTable("terms_acceptances");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, v => new TermsAcceptanceId(v));

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(t => t.Version)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.AcceptedAt).IsRequired();
        builder.Property(t => t.AcceptedFromIp).HasMaxLength(45);
        builder.Property(t => t.UserAgent).HasMaxLength(512);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A user cannot accept the same version of the ToS twice.
        builder.HasIndex(t => new { t.UserId, t.Version }).IsUnique();
    }
}
