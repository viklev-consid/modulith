using System.Text.Json.Serialization;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Login;

public static class GoogleLoginResponseStatus
{
    public const string Authenticated = "authenticated";
    public const string TwoFactorRequired = "twoFactorRequired";
    public const string PendingExternalConfirmation = "pendingExternalConfirmation";
}

public sealed record GoogleLoginResponse(
    string Status,
    GoogleLoginSessionResponse? Session = null,
    GoogleLoginChallengeResponse? Challenge = null)
{
    [JsonIgnore]
    public bool IsPending => string.Equals(Status, GoogleLoginResponseStatus.PendingExternalConfirmation, StringComparison.Ordinal);

    [JsonIgnore]
    public string? AccessToken => Session?.AccessToken;

    [JsonIgnore]
    public string? RefreshToken => Session?.RefreshToken;

    [JsonIgnore]
    public bool RequiresTwoFactor => string.Equals(Status, GoogleLoginResponseStatus.TwoFactorRequired, StringComparison.Ordinal);

    [JsonIgnore]
    public string? TwoFactorChallengeToken => Challenge?.ChallengeToken;

    public static GoogleLoginResponse Authenticated(GoogleLoginSessionResponse session) =>
        new(GoogleLoginResponseStatus.Authenticated, Session: session);

    public static GoogleLoginResponse TwoFactorRequired(GoogleLoginChallengeResponse challenge) =>
        new(GoogleLoginResponseStatus.TwoFactorRequired, Challenge: challenge);

    public static GoogleLoginResponse PendingExternalConfirmation() =>
        new(GoogleLoginResponseStatus.PendingExternalConfirmation);
}

public sealed record GoogleLoginSessionResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record GoogleLoginChallengeResponse(string ChallengeToken, DateTimeOffset ExpiresAt);
