using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Features.Logout;

public sealed class LogoutHandler(UsersDbContext db, IClock clock)
{
    public async Task<ErrorOr<LogoutResponse>> Handle(LogoutCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(LogoutHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<LogoutResponse>> HandleCoreAsync(LogoutCommand cmd, CancellationToken ct)
    {
        var hash = Domain.RefreshToken.HashRawValue(cmd.RawRefreshToken);

        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is not null && token.RevokedAt is null)
        {
            token.Revoke(clock);
            await db.SaveChangesAsync(ct);
        }

        // Always return success — client should discard its token regardless.
        return new LogoutResponse();
    }
}
