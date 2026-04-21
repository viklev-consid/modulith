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
