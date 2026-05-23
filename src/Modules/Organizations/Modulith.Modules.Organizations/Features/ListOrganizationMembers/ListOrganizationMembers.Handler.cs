using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Users.Contracts.Queries;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.ListOrganizationMembers;

public sealed class ListOrganizationMembersHandler(OrganizationsDbContext db, IMessageBus bus)
{
    public async Task<ErrorOr<ListOrganizationMembersResponse>> Handle(ListOrganizationMembersQuery query, CancellationToken ct)
    {
        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == query.OrganizationId && m.IsActive)
            .ToArrayAsync(ct);

        var userIdsToHydrate = memberships
            .Where(m => !m.IsAnonymized && m.UserId is not null)
            .Select(m => m.UserId!.Value)
            .Distinct()
            .ToArray();

        var summariesByUserId = new Dictionary<Guid, UserSummary>();
        if (userIdsToHydrate.Length > 0)
        {
            var summaries = await bus.InvokeAsync<ErrorOr<GetUserSummariesByIdsResponse>>(
                new GetUserSummariesByIdsQuery(userIdsToHydrate),
                ct);

            if (summaries.IsError)
            {
                return summaries.Errors;
            }

            foreach (var summary in summaries.Value.Users)
            {
                summariesByUserId[summary.UserId] = summary;
            }
        }

        var members = memberships
            .OrderBy(m => m.Role.Name, StringComparer.Ordinal)
            .ThenBy(m => m.JoinedAt)
            .Select(m =>
            {
                if (m.IsAnonymized || m.UserId is null || !summariesByUserId.TryGetValue(m.UserId.Value, out var summary))
                {
                    return new OrganizationMemberItem(m.UserId, m.Role.Name, m.JoinedAt, m.IsAnonymized, null, null);
                }

                return new OrganizationMemberItem(m.UserId, m.Role.Name, m.JoinedAt, m.IsAnonymized, summary.DisplayName, summary.Email);
            })
            .ToArray();

        return new ListOrganizationMembersResponse(members);
    }
}
