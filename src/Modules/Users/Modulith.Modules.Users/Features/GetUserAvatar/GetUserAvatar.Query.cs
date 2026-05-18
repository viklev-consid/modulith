namespace Modulith.Modules.Users.Features.GetUserAvatar;

public sealed record GetUserAvatarQuery(Guid TargetUserId, Guid RequestingUserId, string? RequestingRole);
