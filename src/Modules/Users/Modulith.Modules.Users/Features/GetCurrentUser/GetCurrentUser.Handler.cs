using ErrorOr;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.GetCurrentUser;

public sealed class GetCurrentUserHandler(UsersDbContext db)
{
    public async Task<ErrorOr<GetCurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GetCurrentUserHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<GetCurrentUserResponse>> HandleCoreAsync(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([query.UserId], ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        return new GetCurrentUserResponse(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName,
            user.CreatedAt);
    }
}
