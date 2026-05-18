using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.GetUserAvatar;

public sealed class GetUserAvatarHandler(UsersDbContext db, IUserAvatarStorage avatarStorage)
{
    public async Task<ErrorOr<GetUserAvatarResponse>> Handle(GetUserAvatarQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GetUserAvatarHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<GetUserAvatarResponse>> HandleCoreAsync(GetUserAvatarQuery query, CancellationToken ct)
    {
        var canRead = query.TargetUserId == query.RequestingUserId ||
            string.Equals(query.RequestingRole, Role.Admin.Name, StringComparison.Ordinal);

        if (!canRead)
        {
            return UsersErrors.AvatarAccessDenied;
        }

        var targetUserId = new UserId(query.TargetUserId);
        var avatar = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == targetUserId)
            .Select(u => new
            {
                u.AvatarContainer,
                u.AvatarKey,
                u.AvatarContentType,
                u.AvatarUpdatedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (avatar is null)
        {
            return UsersErrors.UserNotFound;
        }

        if (avatar.AvatarContainer is null ||
            avatar.AvatarKey is null ||
            avatar.AvatarContentType is null ||
            avatar.AvatarUpdatedAt is null)
        {
            return UsersErrors.AvatarMissing;
        }

        var content = await avatarStorage.GetAsync(avatar.AvatarContainer, avatar.AvatarKey, ct);
        return new GetUserAvatarResponse(content.Stream, content.Metadata.ContentType, avatar.AvatarUpdatedAt.Value);
    }
}
