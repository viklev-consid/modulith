using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Queries;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.GetUserSummariesByIds;

public sealed class GetUserSummariesByIdsHandler(UsersDbContext db)
{
    public async Task<ErrorOr<GetUserSummariesByIdsResponse>> Handle(
        GetUserSummariesByIdsQuery query,
        CancellationToken ct)
    {
        if (query.UserIds.Count == 0)
        {
            return new GetUserSummariesByIdsResponse([]);
        }

        var ids = query.UserIds.Distinct().Select(id => new UserId(id)).ToArray();

        var summaries = await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserSummary(u.Id.Value, u.Email.Value, u.DisplayName))
            .ToArrayAsync(ct);

        return new GetUserSummariesByIdsResponse(summaries);
    }
}
