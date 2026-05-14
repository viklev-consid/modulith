using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Security;

internal sealed class TwoFactorChallengeIssuer(
    UsersOptionsAccessor optionsAccessor,
    IClock clock) : ITwoFactorChallengeIssuer
{
    public (PendingTwoFactorChallenge challenge, string rawValue) Issue(UserId userId, string? ipAddress) =>
        PendingTwoFactorChallenge.Create(userId, optionsAccessor.Value.TwoFactorChallengeLifetime, clock, ipAddress);
}
