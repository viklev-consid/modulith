namespace Modulith.Modules.Users.Features.LoginTwoFactor;

public sealed record LoginTwoFactorRequest(string ChallengeToken, string Code);
