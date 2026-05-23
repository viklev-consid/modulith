using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Contracts.Commands;
using Modulith.Modules.Organizations.Contracts.Events;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.AcceptOrganizationInvitation;

public sealed class AcceptOrganizationInvitationHandler(OrganizationsDbContext db, IClock clock, IMessageBus bus)
{
    public async Task<ErrorOr<AcceptedOrganizationInvitationForUserResponse>> Handle(AcceptOrganizationInvitationForUserCommand cmd, CancellationToken ct)
    {
        var invitation = await LoadInvitationForTokenAsync(cmd.InvitationToken, ct);
        if (invitation is null)
        {
            return OrganizationsErrors.InvitationInvalid;
        }

        var organization = await db.Organizations
            .Include(o => o.Memberships)
            .FirstOrDefaultAsync(o => o.Id == invitation.OrganizationId, ct);
        if (organization is null || organization.IsDeleted)
        {
            return OrganizationsErrors.OrganizationNotFound;
        }

        var accept = invitation.Accept(cmd.UserId, cmd.Email, clock);
        if (accept.IsError)
        {
            return accept.Errors;
        }

        var add = organization.AddMember(cmd.UserId, invitation.Role, clock);
        if (add.IsError)
        {
            return add.Errors;
        }

        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new OrganizationMemberAddedV1(
            organization.Id.Value,
            cmd.UserId,
            invitation.Role.Name,
            Guid.NewGuid()));

        return new AcceptedOrganizationInvitationForUserResponse(organization.Id.Value, invitation.Role.Name);
    }

    public async Task<ErrorOr<AcceptOrganizationInvitationResponse>> Handle(AcceptOrganizationInvitationCommand cmd, CancellationToken ct)
    {
        var result = await Handle(
            new AcceptOrganizationInvitationForUserCommand(cmd.InvitationToken, cmd.UserId, cmd.Email),
            ct);
        if (result.IsError)
        {
            return result.Errors;
        }

        return new AcceptOrganizationInvitationResponse(result.Value.OrganizationId, result.Value.Role);
    }

    private async Task<OrganizationInvitation?> LoadInvitationForTokenAsync(string rawToken, CancellationToken ct)
    {
        var tokenHash = OrganizationInvitation.HashRawValue(rawToken);
        return await db.Invitations
            .FromSqlInterpolated($"""
                SELECT * FROM organizations.organization_invitations
                WHERE token_hash = {tokenHash}
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(ct);
    }

}
