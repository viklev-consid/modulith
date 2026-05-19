using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security.Authorization;

namespace Modulith.Modules.Users.Features.GetCurrentUser;

public sealed class GetCurrentUserHandler(UsersDbContext db, IPermissionCatalog permissionCatalog)
{
    public async Task<ErrorOr<GetCurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GetCurrentUserHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<GetCurrentUserResponse>> HandleCoreAsync(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == query.UserId, ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var roleName = query.TokenRole ?? user.Role.Name;
        var permissions = permissionCatalog.GetPermissionsForRole(roleName);
        var permissionsVersion = permissionCatalog.GetPermissionsVersion(roleName);
        var twoFactorEnabled = await db.TwoFactorCredentials
            .Where(c => c.UserId == user.Id)
            .WhereActive()
            .AnyAsync(ct);

        return new GetCurrentUserResponse(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName,
            user.CreatedAt,
            roleName,
            permissions,
            permissionsVersion,
            HasCompletedOnboarding: user.HasCompletedOnboarding,
            TwoFactorEnabled: twoFactorEnabled,
            Avatar: user.HasAvatar && user.AvatarUpdatedAt is not null
                ? new CurrentUserAvatarResponse(AvatarUrl.ForUser(user.Id.Value, user.AvatarUpdatedAt.Value), user.AvatarUpdatedAt.Value)
                : null);
    }
}
