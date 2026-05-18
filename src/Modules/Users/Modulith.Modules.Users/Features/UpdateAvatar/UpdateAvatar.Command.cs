namespace Modulith.Modules.Users.Features.UpdateAvatar;

public sealed record UpdateAvatarCommand(Guid UserId, byte[] Content, string ContentType, string? FileName);
