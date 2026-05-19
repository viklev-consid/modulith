namespace Modulith.Modules.Users.Features.GetCurrentUser;

public sealed record GetCurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    string Role,
    IReadOnlyCollection<string> Permissions,
    string PermissionsVersion,
    bool HasCompletedOnboarding,
    bool TwoFactorEnabled,
    CurrentUserAvatarResponse? Avatar);

public sealed record CurrentUserAvatarResponse(
    string Url,
    DateTimeOffset UpdatedAt);
