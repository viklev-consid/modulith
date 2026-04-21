using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.LogoutAll;

public sealed class LogoutAllHandler(UsersDbContext db, IClock clock, IMessageBus bus)
{
    public async Task<ErrorOr<LogoutAllResponse>> Handle(LogoutAllCommand cmd, CancellationToken ct)
    {
        var userId = new UserId(cmd.UserId);

        var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists)
            return UsersErrors.UserNotFound;

        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);

        await bus.PublishAsync(new UserLoggedOutAllDevicesV1(cmd.UserId));

        return new LogoutAllResponse();
    }
}
