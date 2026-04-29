using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Npgsql;
using Wolverine;

namespace Modulith.Modules.Users.Features.ConfirmEmailChange;

public sealed class ConfirmEmailChangeHandler(
    UsersDbContext db,
    ISingleUseTokenService tokenService,
    IClock clock,
    IRefreshTokenRevoker tokenRevoker,
    IMessageBus bus)
{
    public async Task<ErrorOr<ConfirmEmailChangeResponse>> Handle(ConfirmEmailChangeCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ConfirmEmailChangeHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<ConfirmEmailChangeResponse>> HandleCoreAsync(ConfirmEmailChangeCommand cmd, CancellationToken ct)
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

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "ix_users_email" })
        {
            // Between the pre-check in RequestEmailChange and this confirmation committing, someone else
            // claimed the target email. The save was rolled back — token, pending-change, and email
            // mutation are all unwound. Return the same opaque error without revoking any sessions.
            db.ChangeTracker.Clear();
            return UsersErrors.InvalidOrExpiredToken;
        }

        // Revoke all active refresh tokens only after the email change is committed.
        // Running this before SaveChangesAsync would revoke sessions even when the save fails
        // (e.g. email uniqueness race), logging the user out without the change taking effect.
        await tokenRevoker.RevokeAllForUserAsync(user.Id, ct);

        await bus.PublishAsync(new EmailChangedV1(user.Id.Value, oldEmail, user.Email.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(EmailChangedV1)));

        return new ConfirmEmailChangeResponse();
    }
}
