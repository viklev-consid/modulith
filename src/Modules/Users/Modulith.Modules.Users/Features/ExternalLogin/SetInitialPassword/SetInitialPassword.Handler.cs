using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.SetInitialPassword;

public sealed class SetInitialPasswordHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IRefreshTokenRevoker tokenRevoker,
    IGoogleIdTokenVerifier googleVerifier,
    IMessageBus bus)
{
    public async Task<ErrorOr<Success>> Handle(SetInitialPasswordCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(SetInitialPasswordHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(SetInitialPasswordCommand cmd, CancellationToken ct)
    {
        // Step-up: verify the caller controls the Google account currently linked to this user.
        var identityResult = await googleVerifier.VerifyAsync(cmd.GoogleIdToken, ct);
        if (identityResult.IsError)
        {
            return identityResult.Errors;
        }

        var identity = identityResult.Value;

        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        // The presented Google token's subject must match a linked Google login on this account.
        // Return InvalidIdToken for both "no Google link" and "wrong subject" — no oracle.
        var subjectMatches = user.ExternalLogins.Any(e =>
            e.Provider == ExternalLoginProvider.Google &&
            string.Equals(e.Subject, identity.Subject, StringComparison.Ordinal));

        if (!subjectMatches)
        {
            return UsersErrors.InvalidIdToken;
        }

        var hash = new PasswordHash(passwordHasher.Hash(cmd.Password));
        var result = user.SetInitialPassword(hash);
        if (result.IsError)
        {
            return result.Errors;
        }

        // Revoke all refresh tokens — including the current session.
        // A stolen token used to set the initial password must not keep the attacker's session alive.
        await tokenRevoker.RevokeAllForUserAsync(user.Id, ct);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new PasswordChangedV1(user.Id.Value, user.Email.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(PasswordChangedV1)));

        return Result.Success;
    }
}
