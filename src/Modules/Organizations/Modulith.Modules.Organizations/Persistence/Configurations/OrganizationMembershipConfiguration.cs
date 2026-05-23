using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Persistence.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new OrganizationMembershipId(value));

        builder.Property(m => m.OrganizationId)
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        builder.Property(m => m.UserId);

        builder.Property(m => m.Role)
            .HasConversion(role => role.Name, value => OrganizationRole.Create(value).Value)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(m => m.IsActive)
            .IsRequired();

        builder.Property(m => m.IsAnonymized)
            .IsRequired();

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CreatedBy).HasMaxLength(100);
        builder.Property(m => m.UpdatedAt);
        builder.Property(m => m.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(m => new { m.OrganizationId, m.UserId })
            .HasFilter("is_active = true")
            .IsUnique();

        builder.HasIndex(m => m.UserId);
    }
}
