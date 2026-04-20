using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Audit.Domain;

namespace Modulith.Modules.Audit.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new AuditEntryId(v));

        builder.Property(e => e.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ActorId);

        builder.Property(e => e.ResourceType)
            .HasMaxLength(100);

        builder.Property(e => e.ResourceId);

        builder.Property(e => e.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.OccurredAt)
            .IsRequired();

        builder.HasIndex(e => e.ActorId);
        builder.HasIndex(e => e.OccurredAt);
    }
}
