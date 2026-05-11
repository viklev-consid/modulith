using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("user_invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, value => new UserInvitationId(value));

        builder.Property(i => i.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .IsRequired();

        builder.Property(i => i.InvitedByUserId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : new UserId(value.Value));

        builder.Property(i => i.AcceptedUserId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : new UserId(value.Value));

        builder.Property(i => i.CreatedFromIp)
            .HasMaxLength(64);

        builder.Property(i => i.UserAgent)
            .HasMaxLength(512);

        builder.HasIndex(i => i.TokenHash)
            .IsUnique();

        builder.HasIndex(i => i.Email)
            .HasFilter("accepted_at IS NULL AND revoked_at IS NULL")
            .IsUnique();
    }
}
