using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Security;

internal sealed class SingleUseTokenService(UsersDbContext db, IClock clock) : ISingleUseTokenService
{
    public (SingleUseToken token, string rawValue) Create(
        UserId userId,
        TokenPurpose purpose,
        TimeSpan lifetime)
    {
        var (token, rawValue) = SingleUseToken.Create(userId, purpose, lifetime, clock);
        db.SingleUseTokens.Add(token);
        return (token, rawValue);
    }

    public async Task<SingleUseToken?> FindValidAsync(
        string rawToken,
        TokenPurpose purpose,
        CancellationToken ct)
    {
        var hash = SingleUseToken.HashRawValue(rawToken);
        var now = clock.UtcNow;

        return await db.SingleUseTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash
              && t.Purpose == purpose
              && t.ConsumedAt == null
              && t.ExpiresAt > now,
            ct);
    }
}
