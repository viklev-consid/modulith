using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Pagination;

namespace Modulith.Modules.Users.Features.ListUsers;

public sealed class ListUsersHandler(UsersDbContext db)
{
    public async Task<ErrorOr<ListUsersResponse>> Handle(ListUsersQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ListUsersHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<ListUsersResponse>> HandleCoreAsync(ListUsersQuery query, CancellationToken ct)
    {
        if (query.Page <= 0 || query.Page > PageRequest.MaxPage)
            return UsersErrors.PageInvalid;

        if (query.PageSize <= 0 || query.PageSize > PageRequest.MaxPageSize)
            return UsersErrors.PageSizeInvalid;

        var pagination = PageRequest.Of(query.Page, query.PageSize);

        var total = await db.Users.CountAsync(ct);
        var users = await db.Users
            .OrderBy(u => u.CreatedAt)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .Select(u => new ListUsersUserDto(u.Id.Value, u.Email.Value, u.DisplayName, u.Role.Name))
            .ToListAsync(ct);

        return new ListUsersResponse(users, pagination.Page, pagination.PageSize, total);
    }
}
