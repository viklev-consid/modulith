namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;

public sealed record GoogleLoginConfirmResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool IsNewUser);
