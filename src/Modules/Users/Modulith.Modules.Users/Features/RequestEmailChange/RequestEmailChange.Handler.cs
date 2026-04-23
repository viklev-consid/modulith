using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Npgsql;
using Wolverine;

namespace Modulith.Modules.Users.Features.RequestEmailChange;

public sealed class RequestEmailChangeHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    ISingleUseTokenService tokenService,
    IOptions<UsersOptions> options,
    IMessageBus bus)
{
    public async Task<ErrorOr<RequestEmailChangeResponse>> Handle(RequestEmailChangeCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(RequestEmailChangeHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<RequestEmailChangeResponse>> HandleCoreAsync(RequestEmailChangeCommand cmd, CancellationToken ct)
    {
        // Always return the same response to prevent enumeration of email addresses.
        var newEmailResult = Email.Create(cmd.NewEmail);
        if (newEmailResult.IsError)
        {
            return new RequestEmailChangeResponse();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
        {
            return new RequestEmailChangeResponse();
        }

        if (!passwordHasher.Verify(cmd.CurrentPassword, user.PasswordHash.Value))
        {
            return new RequestEmailChangeResponse();
        }

        var newEmail = newEmailResult.Value;

        // If email is already taken, return success shape without sending anything.
        var emailTaken = await db.Users.AnyAsync(u => u.Email == newEmail && u.Id != user.Id, ct);
        if (emailTaken)
        {
            return new RequestEmailChangeResponse();
        }

        // Remove any existing pending change for this user.
        await db.PendingEmailChanges
            .Where(p => p.UserId == user.Id)
            .ExecuteDeleteAsync(ct);

        var (token, rawToken) = tokenService.Create(
            user.Id,
            TokenPurpose.EmailChange,
            options.Value.EmailChangeTokenLifetime);

        db.PendingEmailChanges.Add(PendingEmailChange.Create(user.Id, newEmail, token.Id));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // A concurrent request from the same user hit the unique index on pending_email_changes.user_id.
            // The first request succeeded; silently return the same success shape.
            return new RequestEmailChangeResponse();
        }

        await bus.PublishAsync(new EmailChangeRequestedV1(user.Id.Value, newEmail.Value, rawToken, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(EmailChangeRequestedV1)));

        return new RequestEmailChangeResponse();
    }
}
