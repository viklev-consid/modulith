using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ResetPassword;

public sealed class ResetPasswordHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    ISingleUseTokenService tokenService,
    IClock clock,
    IRefreshTokenRevoker tokenRevoker,
    IMessageBus bus)
{
    public async Task<ErrorOr<ResetPasswordResponse>> Handle(ResetPasswordCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ResetPasswordHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<ResetPasswordResponse>> HandleCoreAsync(ResetPasswordCommand cmd, CancellationToken ct)
    {
        var token = await tokenService.FindValidAsync(cmd.Token, TokenPurpose.PasswordReset, ct);
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

        var newHash = new PasswordHash(passwordHasher.Hash(cmd.NewPassword));
        user.SetPassword(newHash);

        await tokenRevoker.RevokeAllForUserAsync(user.Id, ct);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new PasswordResetV1(user.Id.Value, user.Email.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(PasswordResetV1)));

        return new ResetPasswordResponse();
    }
}
