using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Features.ConfirmEmail;

public sealed class ConfirmEmailHandler(
    UsersDbContext db,
    ISingleUseTokenService tokenService,
    IClock clock)
{
    public async Task<ErrorOr<ConfirmEmailResponse>> Handle(ConfirmEmailCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ConfirmEmailHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<ConfirmEmailResponse>> HandleCoreAsync(ConfirmEmailCommand cmd, CancellationToken ct)
    {
        var token = await tokenService.FindValidAsync(cmd.Token, TokenPurpose.EmailConfirmation, ct);
        if (token is null)
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null)
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var consumeResult = token.Consume(clock);
        if (consumeResult.IsError)
        {
            return consumeResult.Errors;
        }

        user.ConfirmEmail(clock);

        await db.SaveChangesAsync(ct);

        return new ConfirmEmailResponse();
    }
}
