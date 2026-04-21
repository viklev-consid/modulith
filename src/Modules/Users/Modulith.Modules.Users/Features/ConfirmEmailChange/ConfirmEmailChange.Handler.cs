using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ConfirmEmailChange;

public sealed class ConfirmEmailChangeHandler(
    UsersDbContext db,
    ISingleUseTokenService tokenService,
    IClock clock,
    IMessageBus bus)
{
    public async Task<ErrorOr<ConfirmEmailChangeResponse>> Handle(ConfirmEmailChangeCommand cmd, CancellationToken ct)
    {
        var singleUseToken = await tokenService.FindValidAsync(cmd.Token, TokenPurpose.EmailChange, ct);
        if (singleUseToken is null)
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        // Token must belong to the requesting user.
        if (singleUseToken.UserId != new UserId(cmd.UserId))
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var pending = await db.PendingEmailChanges
            .FirstOrDefaultAsync(p => p.UserId == singleUseToken.UserId && p.TokenId == singleUseToken.Id, ct);

        if (pending is null)
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == singleUseToken.UserId, ct);
        if (user is null)
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var consumeResult = singleUseToken.Consume(clock);
        if (consumeResult.IsError)
        {
            return consumeResult.Errors;
        }

        var oldEmail = user.Email.Value;
        var changeResult = user.ChangeEmail(pending.NewEmail);
        if (changeResult.IsError)
        {
            return changeResult.Errors;
        }

        db.PendingEmailChanges.Remove(pending);

        // Revoke all refresh tokens — email change is a security event.
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new EmailChangedV1(user.Id.Value, oldEmail, user.Email.Value));

        return new ConfirmEmailChangeResponse();
    }
}
