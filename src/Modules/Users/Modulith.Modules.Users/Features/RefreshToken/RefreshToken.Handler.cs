using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Features.RefreshToken;

public sealed class RefreshTokenHandler(
    UsersDbContext db,
    IJwtGenerator jwtGenerator,
    IRefreshTokenIssuer refreshTokenIssuer,
    IOptions<UsersOptions> options,
    IClock clock)
{
    public async Task<ErrorOr<RefreshTokenResponse>> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var hash = Domain.RefreshToken.HashRawValue(cmd.RawRefreshToken);
        var now = clock.UtcNow;

        var existing = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
            return UsersErrors.RefreshTokenNotFound;

        // Reuse detection: a rotated token being presented again means the old token
        // was stolen. Revoke the entire chain and force re-login.
        if (existing.RevokedAt is not null && existing.RotatedTo is not null)
        {
            await db.RefreshTokens
                .Where(t => t.UserId == existing.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

            await db.SaveChangesAsync(ct);
            return UsersErrors.RefreshTokenRevoked;
        }

        if (existing.RevokedAt is not null)
            return UsersErrors.RefreshTokenRevoked;

        if (existing.ExpiresAt <= now)
            return UsersErrors.RefreshTokenExpired;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);
        if (user is null)
            return UsersErrors.UserNotFound;

        // Issue replacement token before marking the old one as rotated.
        var (newRefreshToken, rawNewRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);

        existing.MarkRotatedTo(newRefreshToken.Id, clock);

        await db.SaveChangesAsync(ct);

        var accessTokenExpiresAt = clock.UtcNow.AddMinutes(options.Value.AccessTokenLifetimeMinutes);
        var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, newRefreshToken.Id.Value);

        return new RefreshTokenResponse(
            accessToken,
            accessTokenExpiresAt,
            rawNewRefreshToken,
            newRefreshToken.ExpiresAt);
    }
}
