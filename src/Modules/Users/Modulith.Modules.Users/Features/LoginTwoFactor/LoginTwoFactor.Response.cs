namespace Modulith.Modules.Users.Features.LoginTwoFactor;

public sealed record LoginTwoFactorResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
