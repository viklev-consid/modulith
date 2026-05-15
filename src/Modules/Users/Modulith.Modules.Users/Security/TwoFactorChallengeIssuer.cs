using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Security;

internal sealed class TwoFactorChallengeIssuer(
    IOptions<UsersOptions> options,
    IClock clock) : ITwoFactorChallengeIssuer
{
    public (PendingTwoFactorChallenge challenge, string rawValue) Issue(UserId userId, string? ipAddress) =>
        PendingTwoFactorChallenge.Create(userId, options.Value.TwoFactorChallengeLifetime, clock, ipAddress);
}
