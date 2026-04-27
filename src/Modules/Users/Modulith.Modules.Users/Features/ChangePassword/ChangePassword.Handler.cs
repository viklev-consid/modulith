using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Wolverine;

namespace Modulith.Modules.Users.Features.ChangePassword;

public sealed class ChangePasswordHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IRefreshTokenRevoker tokenRevoker,
    IMessageBus bus)
{
    public async Task<ErrorOr<ChangePasswordResponse>> Handle(ChangePasswordCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ChangePasswordHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<ChangePasswordResponse>> HandleCoreAsync(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        if (user.PasswordHash is null || !passwordHasher.Verify(cmd.CurrentPassword, user.PasswordHash.Value))
        {
            return UsersErrors.CurrentPasswordIncorrect;
        }

        var newHash = new PasswordHash(passwordHasher.Hash(cmd.NewPassword));
        user.SetPassword(newHash);

        RefreshTokenId? keepId = null;
        if (cmd.ActiveRefreshTokenId is not null && Guid.TryParse(cmd.ActiveRefreshTokenId, out var parsed))
        {
            keepId = new RefreshTokenId(parsed);
        }

        await tokenRevoker.RevokeAllForUserAsync(user.Id, ct, except: keepId);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new PasswordChangedV1(user.Id.Value, user.Email.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(PasswordChangedV1)));

        return new ChangePasswordResponse();
    }
}
