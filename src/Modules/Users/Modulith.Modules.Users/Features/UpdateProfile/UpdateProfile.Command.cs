namespace Modulith.Modules.Users.Features.UpdateProfile;

public sealed record UpdateProfileCommand(Guid UserId, string DisplayName);
