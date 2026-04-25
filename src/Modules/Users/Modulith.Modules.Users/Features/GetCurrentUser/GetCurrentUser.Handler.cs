using ErrorOr;
using Microsoft.EntityFrameworkCore;
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
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == query.UserId, ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var roleName = query.TokenRole ?? user.Role.Name;
        var permissions = permissionCatalog.GetPermissionsForRole(roleName);
        var permissionsVersion = permissionCatalog.GetPermissionsVersion(roleName);
        var linkedProviders = user.ExternalLogins.Select(e => e.Provider.ToString()).ToList();

        return new GetCurrentUserResponse(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName,
            user.CreatedAt,
            roleName,
            permissions,
            permissionsVersion,
            HasPassword: user.PasswordHash is not null,
            HasCompletedOnboarding: user.HasCompletedOnboarding,
            LinkedProviders: linkedProviders);
    }
}
