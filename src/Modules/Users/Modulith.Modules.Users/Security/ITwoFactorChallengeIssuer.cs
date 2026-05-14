using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Security;

public interface ITwoFactorChallengeIssuer
{
    (PendingTwoFactorChallenge challenge, string rawValue) Issue(UserId userId, string? ipAddress);
}
