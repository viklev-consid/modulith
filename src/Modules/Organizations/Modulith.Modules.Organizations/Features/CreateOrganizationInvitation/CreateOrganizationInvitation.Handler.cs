using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Contracts.Events;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.CreateOrganizationInvitation;

public sealed class CreateOrganizationInvitationHandler(OrganizationsDbContext db, IClock clock, IMessageBus bus)
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(14);

    public async Task<ErrorOr<CreateOrganizationInvitationResponse>> Handle(CreateOrganizationInvitationCommand cmd, CancellationToken ct)
    {
        var role = OrganizationRole.Create(cmd.Role);
        if (role.IsError)
        {
            return role.Errors;
        }

        var organization = await db.Organizations
            .Include(o => o.Memberships)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrganizationId, ct);
        if (organization is null || organization.IsDeleted)
        {
            return OrganizationsErrors.OrganizationNotFound;
        }

        var normalizedEmail = cmd.Email.Trim().ToLowerInvariant();
        if (await db.Invitations.AnyAsync(i => i.OrganizationId == cmd.OrganizationId && i.Email == normalizedEmail && i.IsPending, ct))
        {
            return OrganizationsErrors.InvitationInvalid;
        }

        var invitation = OrganizationInvitation.Create(cmd.OrganizationId, normalizedEmail, role.Value, InvitationLifetime, cmd.InvitedByUserId, clock);
        if (invitation.IsError)
        {
            return invitation.Errors;
        }

        db.Invitations.Add(invitation.Value.Invitation);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new OrganizationInvitationCreatedV1(
            cmd.OrganizationId.Value,
            invitation.Value.Invitation.Id.Value,
            normalizedEmail,
            role.Value.Name,
            invitation.Value.RawToken,
            cmd.InvitedByUserId,
            Guid.NewGuid()));

        return new CreateOrganizationInvitationResponse(
            invitation.Value.Invitation.Id.Value,
            normalizedEmail,
            role.Value.Name,
            invitation.Value.Invitation.ExpiresAt,
            invitation.Value.RawToken);
    }
}
