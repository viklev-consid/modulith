using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Wolverine;

namespace Modulith.Modules.Users.Features.DeleteAvatar;

public sealed class DeleteAvatarHandler(UsersDbContext db, IUserAvatarStorage avatarStorage, IMessageBus bus)
{
    public async Task<ErrorOr<Deleted>> Handle(DeleteAvatarCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(DeleteAvatarHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Deleted>> HandleCoreAsync(DeleteAvatarCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var previous = user.RemoveAvatar();
        await db.SaveChangesAsync(ct);
        await avatarStorage.DeleteAsync(previous.Container, previous.Key, ct);
        if (previous.Key is not null)
        {
            await bus.PublishAsync(new UserAvatarRemovedV1(user.Id.Value, Guid.NewGuid()));
            UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserAvatarRemovedV1)));
        }

        return Result.Deleted;
    }
}
