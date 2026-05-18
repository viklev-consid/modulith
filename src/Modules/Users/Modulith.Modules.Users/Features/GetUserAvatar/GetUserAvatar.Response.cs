namespace Modulith.Modules.Users.Features.GetUserAvatar;

public sealed record GetUserAvatarResponse(Stream Content, string ContentType, DateTimeOffset UpdatedAt);
