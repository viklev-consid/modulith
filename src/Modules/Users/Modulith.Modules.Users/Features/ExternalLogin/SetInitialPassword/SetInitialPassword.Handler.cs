using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.SetInitialPassword;

public sealed class SetInitialPasswordHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IRefreshTokenRevoker tokenRevoker,
    IMessageBus bus)
{
    public async Task<ErrorOr<Success>> Handle(SetInitialPasswordCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(SetInitialPasswordHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(SetInitialPasswordCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var hash = new PasswordHash(passwordHasher.Hash(cmd.Password));
        var result = user.SetInitialPassword(hash);
        if (result.IsError)
        {
            return result.Errors;
        }

        RefreshTokenId? keepId = null;
        if (cmd.ActiveRefreshTokenId is not null && Guid.TryParse(cmd.ActiveRefreshTokenId, out var parsed))
        {
            keepId = new RefreshTokenId(parsed);
        }

        await tokenRevoker.RevokeAllForUserAsync(user.Id, ct, except: keepId);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new PasswordChangedV1(user.Id.Value, user.Email.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(PasswordChangedV1)));

        return Result.Success;
    }
}
