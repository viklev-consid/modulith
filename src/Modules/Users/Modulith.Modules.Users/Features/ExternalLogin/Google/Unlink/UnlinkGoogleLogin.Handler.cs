using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Unlink;

public sealed class UnlinkGoogleLoginHandler(UsersDbContext db, IRefreshTokenRevoker tokenRevoker, IMessageBus bus)
{
    public async Task<ErrorOr<Success>> Handle(UnlinkGoogleLoginCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(UnlinkGoogleLoginHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(UnlinkGoogleLoginCommand cmd, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var unlinkResult = user.UnlinkExternalLogin(ExternalLoginProvider.Google);
        if (unlinkResult.IsError)
        {
            return unlinkResult.Errors;
        }

        await tokenRevoker.RevokeAllForUserAsync(user.Id, ct);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new ExternalLoginUnlinkedV1(user.Id.Value, user.Email.Value, "Google", Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(ExternalLoginUnlinkedV1)));

        return Result.Success;
    }
}
