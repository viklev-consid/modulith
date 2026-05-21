using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Persistence.Configurations;

internal sealed class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
    {
        builder.ToTable("organization_invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, value => new OrganizationInvitationId(value));

        builder.Property(i => i.OrganizationId)
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        builder.Property(i => i.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(i => i.Role)
            .HasConversion(role => role.Name, value => OrganizationRole.Create(value).Value)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .IsRequired();

        builder.Property(i => i.IsPending)
            .IsRequired();

        builder.HasIndex(i => i.TokenHash)
            .IsUnique();

        builder.HasIndex(i => new { i.OrganizationId, i.Email })
            .HasFilter("is_pending = true")
            .IsUnique();
    }
}
