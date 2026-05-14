namespace Modulith.Modules.Users.Features.Login;

public sealed record LoginResponse(
    Guid? UserId = null,
    string? AccessToken = null,
    DateTimeOffset? AccessTokenExpiresAt = null,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null,
    bool RequiresTwoFactor = false,
    string? TwoFactorChallengeToken = null,
    DateTimeOffset? TwoFactorChallengeExpiresAt = null);
