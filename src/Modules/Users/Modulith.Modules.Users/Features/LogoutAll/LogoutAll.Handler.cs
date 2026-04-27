using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Wolverine;

namespace Modulith.Modules.Users.Features.LogoutAll;

public sealed class LogoutAllHandler(UsersDbContext db, IRefreshTokenRevoker tokenRevoker, IMessageBus bus)
{
    public async Task<ErrorOr<LogoutAllResponse>> Handle(LogoutAllCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(LogoutAllHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<LogoutAllResponse>> HandleCoreAsync(LogoutAllCommand cmd, CancellationToken ct)
    {
        var userId = new UserId(cmd.UserId);

        var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists)
        {
            return UsersErrors.UserNotFound;
        }

        await tokenRevoker.RevokeAllForUserAsync(userId, ct);

        await bus.PublishAsync(new UserLoggedOutAllDevicesV1(cmd.UserId));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserLoggedOutAllDevicesV1)));

        return new LogoutAllResponse();
    }
}
