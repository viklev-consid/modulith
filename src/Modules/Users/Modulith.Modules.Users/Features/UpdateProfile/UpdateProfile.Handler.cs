using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.UpdateProfile;

public sealed class UpdateProfileHandler(UsersDbContext db)
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

        var updateResult = user.UpdateProfile(cmd.DisplayName);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        await db.SaveChangesAsync(ct);

        return new UpdateProfileResponse(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName);
    }
}
