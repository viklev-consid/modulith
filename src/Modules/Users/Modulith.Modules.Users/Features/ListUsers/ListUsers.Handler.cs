using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.ListUsers;

public sealed class ListUsersHandler(UsersDbContext db)
{
    public async Task<ErrorOr<ListUsersResponse>> Handle(ListUsersQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ListUsersHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<ListUsersResponse>> HandleCoreAsync(ListUsersQuery query, CancellationToken ct)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);

        var total = await db.Users.CountAsync(ct);
        var users = await db.Users
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new ListUsersUserDto(u.Id.Value, u.Email.Value, u.DisplayName, u.Role.Name))
            .ToListAsync(ct);

        return new ListUsersResponse(users, page, pageSize, total);
    }
}
