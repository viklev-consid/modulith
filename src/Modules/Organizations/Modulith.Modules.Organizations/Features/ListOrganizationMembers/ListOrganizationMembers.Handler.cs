using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations.Features.ListOrganizationMembers;

public sealed class ListOrganizationMembersHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<ListOrganizationMembersResponse>> Handle(ListOrganizationMembersQuery query, CancellationToken ct)
    {
        var members = await db.Memberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == query.OrganizationId && m.IsActive)
            .OrderBy(m => m.Role.Name)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new OrganizationMemberItem(m.UserId, m.Role.Name, m.JoinedAt, m.IsAnonymized))
            .ToArrayAsync(ct);

        return new ListOrganizationMembersResponse(members);
    }
}
