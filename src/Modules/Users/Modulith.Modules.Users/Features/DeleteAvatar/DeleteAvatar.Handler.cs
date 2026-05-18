using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.DeleteAvatar;

public sealed class DeleteAvatarHandler(UsersDbContext db, IUserAvatarStorage avatarStorage)
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

        return Result.Deleted;
    }
}
