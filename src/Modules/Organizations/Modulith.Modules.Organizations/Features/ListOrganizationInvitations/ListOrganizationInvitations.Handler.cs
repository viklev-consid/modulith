using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations.Features.ListOrganizationInvitations;

public sealed class ListOrganizationInvitationsHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<ListOrganizationInvitationsResponse>> Handle(ListOrganizationInvitationsQuery query, CancellationToken ct)
    {
        var invitations = await db.Invitations
            .AsNoTracking()
            .Where(i => i.OrganizationId == query.OrganizationId)
            .OrderByDescending(i => i.InvitedAt)
            .Select(i => new OrganizationInvitationItem(i.Id.Value, i.Email, i.Role.Name, i.ExpiresAt, i.IsPending))
            .ToArrayAsync(ct);

        return new ListOrganizationInvitationsResponse(invitations);
    }
}
