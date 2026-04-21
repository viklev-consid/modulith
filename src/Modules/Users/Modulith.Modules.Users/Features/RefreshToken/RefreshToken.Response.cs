namespace Modulith.Modules.Users.Features.RefreshToken;

public sealed record RefreshTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
