using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class ConsentConfiguration : IEntityTypeConfiguration<Consent>
{
    public void Configure(EntityTypeBuilder<Consent> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, v => new ConsentId(v));

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.ConsentKey).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Granted).IsRequired();
        builder.Property(c => c.RecordedAt).IsRequired();

        builder.HasIndex(c => new { c.UserId, c.ConsentKey });
    }
}
