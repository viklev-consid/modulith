using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Security;

internal sealed class RefreshTokenIssuer(
    UsersDbContext db,
    IOptions<UsersOptions> options,
    IHttpContextAccessor httpContextAccessor,
    IClock clock) : IRefreshTokenIssuer
{
    public async Task<(RefreshToken token, string rawValue)> IssueAsync(UserId userId, CancellationToken ct)
    {
        var limit = options.Value.MaxActiveRefreshTokensPerUser;
        var now = clock.UtcNow;

        // Serialise concurrent token issuance for this user. Without this lock, two concurrent
        // logins both read the same active count, both decide there is room, and both insert —
        // exceeding the cap by one. The lock is transaction-scoped (pg_advisory_xact_lock would
        // also work, but FOR UPDATE on the user row is more idiomatic here and ties the lock
        // lifetime to the EF/Wolverine transaction that wraps the handler).
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM users.users WHERE id = {userId.Value} FOR UPDATE", ct);

        var activeCount = await db.RefreshTokens
            .CountAsync(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now, ct);

        if (activeCount >= limit)
        {
            var oldest = await db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
                .OrderBy(t => t.IssuedAt)
                .FirstAsync(ct);

            oldest.Revoke(clock);
        }

        var ua = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        var (token, rawValue) = RefreshToken.Issue(
            userId,
            TimeSpan.FromDays(options.Value.RefreshTokenLifetimeDays),
            clock,
            ua,
            ip);

        db.RefreshTokens.Add(token);

        return (token, rawValue);
    }
}
