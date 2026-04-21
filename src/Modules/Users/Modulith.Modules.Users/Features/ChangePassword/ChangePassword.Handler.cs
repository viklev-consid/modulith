using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ChangePassword;

public sealed class ChangePasswordHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IClock clock,
    IMessageBus bus)
{
    public async Task<ErrorOr<ChangePasswordResponse>> Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
            return UsersErrors.UserNotFound;

        if (!passwordHasher.Verify(cmd.CurrentPassword, user.PasswordHash.Value))
            return UsersErrors.CurrentPasswordIncorrect;

        var newHash = new PasswordHash(passwordHasher.Hash(cmd.NewPassword));
        user.SetPassword(newHash);

        // Revoke all refresh tokens except the one on the current request (user's session continues).
        var query = db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null);

        if (cmd.ActiveRefreshTokenId is not null && Guid.TryParse(cmd.ActiveRefreshTokenId, out var keepId))
            query = query.Where(t => t.Id != new RefreshTokenId(keepId));

        await query.ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new PasswordChangedV1(user.Id.Value, user.Email.Value));

        return new ChangePasswordResponse();
    }
}
