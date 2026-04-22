using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security.Authorization;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ChangeUserRole;

public sealed class ChangeUserRoleHandler(
    UsersDbContext db,
    IClock clock,
    IMessageBus bus,
    IPermissionCatalog permissionCatalog)
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

        // Resolve canonical role name from the catalog. Rejects unknown roles (e.g. "moderator")
        // and normalises casing so callers cannot persist "ADMIN" or "User" variants.
        var canonicalRole = permissionCatalog.ResolveRole(cmd.NewRole);
        if (canonicalRole is null)
        {
            return UsersErrors.RoleNotFound;
        }

        var newRole = new Role(canonicalRole);

        var user = await db.Users.FindAsync([targetUserId], ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var oldRoleName = user.Role.Name;
        var changeResult = user.ChangeRole(newRole, changedBy);
        if (changeResult.IsError)
        {
            return changeResult.Errors;
        }

        // Revoke all active refresh tokens so the user is forced to re-login
        // and receives a token carrying the new role.
        await db.RefreshTokens
            .Where(t => t.UserId == targetUserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request modified this user row between our read and our write.
            // The domain event was raised in memory but is discarded — Wolverine's outbox
            // write is part of the same transaction, so it was never committed.
            return UsersErrors.ConcurrencyConflict;
        }

        await bus.PublishAsync(new UserRoleChangedV1(
            cmd.TargetUserId,
            oldRoleName,
            newRole.Name,
            cmd.ChangedBy));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRoleChangedV1)));

        return new ChangeUserRoleResponse(cmd.TargetUserId, newRole.Name);
    }
}
