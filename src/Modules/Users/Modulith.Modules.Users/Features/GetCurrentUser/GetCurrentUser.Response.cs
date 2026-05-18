namespace Modulith.Modules.Users.Features.GetCurrentUser;

public sealed record GetCurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    string Role,
    IReadOnlyCollection<string> Permissions,
    string PermissionsVersion,
    bool HasPassword,
    bool HasCompletedOnboarding,
    bool TwoFactorEnabled,
    CurrentUserAvatarResponse? Avatar,
    IReadOnlyCollection<LinkedAccountResponse> LinkedAccounts);

public sealed record LinkedAccountResponse(
    string Provider,
    string ProviderEmail);

public sealed record CurrentUserAvatarResponse(
    string Url,
    DateTimeOffset UpdatedAt);
