namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Login;

public sealed record GoogleLoginResponse(
    bool IsPending,
    Guid? UserId = null,
    string? AccessToken = null,
    DateTimeOffset? AccessTokenExpiresAt = null,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null,
    bool RequiresTwoFactor = false,
    string? TwoFactorChallengeToken = null,
    DateTimeOffset? TwoFactorChallengeExpiresAt = null);
