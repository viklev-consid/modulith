using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.UpdateAvatar;

public sealed class UpdateAvatarHandler(
    UsersDbContext db,
    IAvatarImageInspector validator,
    IUserAvatarStorage avatarStorage,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<UpdateAvatarResponse>> Handle(UpdateAvatarCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(UpdateAvatarHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<UpdateAvatarResponse>> HandleCoreAsync(UpdateAvatarCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        await using var content = new MemoryStream(cmd.Content, writable: false);
        var validation = await validator.ValidateAsync(content, cmd.ContentType, cmd.Content.LongLength, ct);
        if (validation.IsError)
        {
            return validation.Errors;
        }

        var stored = await avatarStorage.StoreAsync(content, validation.Value, ct);
        var previous = user.SetAvatar(
            stored.BlobRef.Container,
            stored.BlobRef.Key,
            stored.ContentType,
            stored.SizeBytes,
            clock);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await avatarStorage.DeleteAsync(stored.BlobRef.Container, stored.BlobRef.Key, ct);
            throw;
        }

        await avatarStorage.DeleteAsync(previous.Container, previous.Key, ct);

        await bus.PublishAsync(new UserAvatarUpdatedV1(user.Id.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserAvatarUpdatedV1)));

        return new UpdateAvatarResponse(
            AvatarUrl.ForUser(user.Id.Value, user.AvatarUpdatedAt!.Value),
            user.AvatarUpdatedAt!.Value);
    }
}
