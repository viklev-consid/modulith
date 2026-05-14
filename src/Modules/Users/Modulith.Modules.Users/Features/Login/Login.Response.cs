namespace Modulith.Modules.Users.Features.Login;

public sealed record LoginResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset? AccessTokenExpiresAt = null,
    string RefreshToken = "",
    DateTimeOffset? RefreshTokenExpiresAt = null,
    bool RequiresTwoFactor = false,
    string TwoFactorChallengeToken = "",
    DateTimeOffset? TwoFactorChallengeExpiresAt = null);
