using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ChangeUserRole;

public sealed class ChangeUserRoleHandler(UsersDbContext db, IClock clock, IMessageBus bus)
{
    public async Task<ErrorOr<ChangeUserRoleResponse>> Handle(ChangeUserRoleCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ChangeUserRoleHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<ChangeUserRoleResponse>> HandleCoreAsync(ChangeUserRoleCommand cmd, CancellationToken ct)
    {
        var targetUserId = new UserId(cmd.TargetUserId);
        var changedBy = new UserId(cmd.ChangedBy);

        // Prevent admin from changing their own role — avoids the last-admin footgun.
        if (cmd.TargetUserId == cmd.ChangedBy)
        {
            return UsersErrors.CannotChangeSelfRole;
        }

        var user = await db.Users.FindAsync([targetUserId], ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var roleResult = Role.Create(cmd.NewRole);
        if (roleResult.IsError)
        {
            return UsersErrors.RoleNotFound;
        }

        var oldRoleName = user.Role.Name;
        var changeResult = user.ChangeRole(roleResult.Value, changedBy);
        if (changeResult.IsError)
        {
            return changeResult.Errors;
        }

        // Revoke all active refresh tokens so the user is forced to re-login
        // and receives a token carrying the new role.
        await db.RefreshTokens
            .Where(t => t.UserId == targetUserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new UserRoleChangedV1(
            cmd.TargetUserId,
            oldRoleName,
            roleResult.Value.Name,
            cmd.ChangedBy));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRoleChangedV1)));

        return new ChangeUserRoleResponse(cmd.TargetUserId, roleResult.Value.Name);
    }
}
