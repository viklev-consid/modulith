namespace Modulith.Modules.Users.Features.LoginTwoFactor;

public sealed record LoginTwoFactorCommand(string ChallengeToken, string Code, string? IpAddress);
