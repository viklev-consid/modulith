using System.Text.Json.Serialization;

namespace Modulith.Modules.Users.Features.Login;

public static class LoginResponseStatus
{
    public const string Authenticated = "authenticated";
    public const string TwoFactorRequired = "twoFactorRequired";
}

public sealed record LoginResponse(string Status, LoginSessionResponse? Session = null, LoginChallengeResponse? Challenge = null)
{
    [JsonIgnore]
    public Guid UserId => Session?.UserId ?? Guid.Empty;

    [JsonIgnore]
    public string AccessToken => Session?.AccessToken ?? string.Empty;

    [JsonIgnore]
    public string RefreshToken => Session?.RefreshToken ?? string.Empty;

    [JsonIgnore]
    public bool RequiresTwoFactor => string.Equals(Status, LoginResponseStatus.TwoFactorRequired, StringComparison.Ordinal);

    [JsonIgnore]
    public string TwoFactorChallengeToken => Challenge?.ChallengeToken ?? string.Empty;

    public static LoginResponse Authenticated(LoginSessionResponse session) =>
        new(LoginResponseStatus.Authenticated, Session: session);

    public static LoginResponse TwoFactorRequired(LoginChallengeResponse challenge) =>
        new(LoginResponseStatus.TwoFactorRequired, Challenge: challenge);
}

public sealed record LoginSessionResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record LoginChallengeResponse(string ChallengeToken, DateTimeOffset ExpiresAt);
