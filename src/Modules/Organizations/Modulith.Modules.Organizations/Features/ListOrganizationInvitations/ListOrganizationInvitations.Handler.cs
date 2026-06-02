using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Shared.Kernel.Pagination;

namespace Modulith.Modules.Organizations.Features.ListOrganizationInvitations;

public sealed class ListOrganizationInvitationsHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<ListOrganizationInvitationsResponse>> Handle(ListOrganizationInvitationsQuery query, CancellationToken ct)
    {
        if (query.PageSize <= 0 || query.PageSize > PageRequest.MaxPageSize)
        {
            return OrganizationsErrors.PageSizeInvalid;
        }

        var pagination = PageRequest.Of(query.Page, query.PageSize);
        var baseQuery = db.Invitations
            .AsNoTracking()
            .Where(i => i.OrganizationId == query.OrganizationId);
        var total = await baseQuery.CountAsync(ct);
        var invitations = await baseQuery
            .OrderByDescending(i => i.InvitedAt)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .Select(i => new OrganizationInvitationItem(i.Id.Value, i.Email, i.Role.Name, i.ExpiresAt, i.IsPending))
            .ToArrayAsync(ct);

        return new ListOrganizationInvitationsResponse(invitations, pagination.Page, pagination.PageSize, total);
    }
}
