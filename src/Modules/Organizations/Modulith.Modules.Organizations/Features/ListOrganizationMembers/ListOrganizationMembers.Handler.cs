using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Users.Contracts.Queries;
using Modulith.Shared.Kernel.Pagination;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.ListOrganizationMembers;

public sealed class ListOrganizationMembersHandler(OrganizationsDbContext db, IMessageBus bus)
{
    public async Task<ErrorOr<ListOrganizationMembersResponse>> Handle(ListOrganizationMembersQuery query, CancellationToken ct)
    {
        if (query.PageSize <= 0 || query.PageSize > PageRequest.MaxPageSize)
        {
            return OrganizationsErrors.PageSizeInvalid;
        }

        var pagination = PageRequest.Of(query.Page, query.PageSize);
        var baseQuery = db.Memberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == query.OrganizationId && m.IsActive);
        var total = await baseQuery.CountAsync(ct);
        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == query.OrganizationId && m.IsActive)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
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

        return new ListOrganizationMembersResponse(members, pagination.Page, pagination.PageSize, total);
    }
}
