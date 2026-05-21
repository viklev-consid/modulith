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
    public async Task<ErrorOr<Success>> Handle(AcceptOrganizationInvitationForUserCommand cmd, CancellationToken ct)
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

        return Result.Success;
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

        var invitation = await LoadAcceptedInvitationForUserAsync(cmd.UserId, ct);
        if (invitation is null)
        {
            return OrganizationsErrors.InvitationInvalid;
        }

        return new AcceptOrganizationInvitationResponse(invitation.OrganizationId.Value, invitation.Role.Name);
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

    private async Task<OrganizationInvitation?> LoadAcceptedInvitationForUserAsync(Guid userId, CancellationToken ct) =>
        await db.Invitations
            .AsNoTracking()
            .Where(i => i.AcceptedUserId == userId && i.AcceptedAt != null)
            .OrderByDescending(i => i.AcceptedAt)
            .FirstOrDefaultAsync(ct);
}
