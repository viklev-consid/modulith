using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Gdpr;

public sealed class UsersPersonalDataEraser(UsersDbContext db) : IPersonalDataEraser
{
    public async Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct)
    {
        var affected = 0;

        var userId = new UserId(user.UserId);

        affected += await db.RefreshTokens
            .Where(t => t.UserId == userId)
            .ExecuteDeleteAsync(ct);

        affected += await db.SingleUseTokens
            .Where(t => t.UserId == userId)
            .ExecuteDeleteAsync(ct);

        affected += await db.PendingEmailChanges
            .Where(p => p.UserId == userId)
            .ExecuteDeleteAsync(ct);

        var dbUser = await db.Users.FindAsync([userId], ct);
        if (dbUser is not null)
        {
            db.Users.Remove(dbUser);
            affected++;
        }

        var consents = await db.Consents
            .Where(c => c.UserId == user.UserId)
            .ToListAsync(ct);

        db.Consents.RemoveRange(consents);
        affected += consents.Count;

        await db.SaveChangesAsync(ct);

        return new ErasureResult(user.UserId, ErasureStrategy.HardDelete, affected);
    }
}
