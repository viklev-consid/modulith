using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Wolverine;

namespace Modulith.Modules.Users.Features.UpdateProfile;

public sealed class UpdateProfileHandler(UsersDbContext db, IMessageBus bus)
{
    public async Task<ErrorOr<UpdateProfileResponse>> Handle(UpdateProfileCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(UpdateProfileHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<UpdateProfileResponse>> HandleCoreAsync(UpdateProfileCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var oldDisplayName = user.DisplayName;
        var updateResult = user.UpdateProfile(cmd.DisplayName);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        await db.SaveChangesAsync(ct);

        if (!string.Equals(oldDisplayName, user.DisplayName, StringComparison.Ordinal))
        {
            await bus.PublishAsync(new UserProfileUpdatedV1(
                user.Id.Value,
                oldDisplayName,
                user.DisplayName,
                Guid.NewGuid()));
            UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserProfileUpdatedV1)));
        }

        return new UpdateProfileResponse(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName);
    }
}
