using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Security;

public sealed class RefreshTokenRevoker(UsersDbContext db, IClock clock) : IRefreshTokenRevoker
{
    public async Task RevokeAllForUserAsync(UserId userId, CancellationToken ct, RefreshTokenId? except = null)
    {
        var query = db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null);

        if (except is not null)
        {
            query = query.Where(t => t.Id != except);
        }

        await query.ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);
    }
}
