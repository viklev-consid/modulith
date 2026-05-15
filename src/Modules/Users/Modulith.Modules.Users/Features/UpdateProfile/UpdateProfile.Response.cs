namespace Modulith.Modules.Users.Features.UpdateProfile;

public sealed record UpdateProfileResponse(
    Guid UserId,
    string Email,
    string DisplayName);
