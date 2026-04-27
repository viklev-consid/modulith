using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Npgsql;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;

public sealed class GoogleLoginConfirmHandler(
    UsersDbContext db,
    IJwtGenerator jwtGenerator,
    IRefreshTokenIssuer refreshTokenIssuer,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<GoogleLoginConfirmResponse>> Handle(GoogleLoginConfirmCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GoogleLoginConfirmHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<GoogleLoginConfirmResponse>> HandleCoreAsync(GoogleLoginConfirmCommand cmd, CancellationToken ct)
    {
        var tokenHash = PendingExternalLogin.HashRawValue(cmd.Token);

        // Lock the row inside the Wolverine-managed transaction so that two concurrent
        // confirm requests for the same token block until the first one commits.
        // The second reader then sees ConsumedAt populated and returns InvalidOrExpiredToken.
        var pending = await db.PendingExternalLogins
            .FromSqlInterpolated($"""
                SELECT * FROM users.pending_external_logins
                WHERE token_hash = {tokenHash}
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(ct);

        if (pending is null)
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var consumeResult = pending.Consume(clock);
        if (consumeResult.IsError)
        {
            return consumeResult.Errors;
        }

        var emailResult = Email.Create(pending.Email);
        if (emailResult.IsError)
        {
            return UsersErrors.ExternalAuthUnavailable;
        }

        var email = emailResult.Value;
        var opts = options.Value;
        var now = clock.UtcNow;

        // Re-query live state under lock — IsExistingUser was snapshotted at pending-creation time
        // and may be stale (e.g. the user registered with a password between login and confirm).
        // Lock the user row so that two concurrent confirms for the same email+provider+subject
        // cannot both pass LinkExternalLogin's in-memory check and then race into SaveChanges.
        // Explicit column list (not SELECT *) is required: xmin is a PostgreSQL system column and
        // is not exposed by SELECT * from a subquery, but EF Core needs it for the concurrency token.
        var existingUser = await LoadUserForEmailAsync(email, ct);

        if (existingUser is not null)
        {
            return await LinkToExistingUserAsync(existingUser, pending, cmd, opts, now, ct);
        }

        return await ProvisionNewUserAsync(email, pending, cmd, opts, now, ct);
    }

    private async Task<ErrorOr<GoogleLoginConfirmResponse>> LinkToExistingUserAsync(
        User user,
        PendingExternalLogin pending,
        GoogleLoginConfirmCommand cmd,
        UsersOptions opts,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var linkResult = user.LinkExternalLogin(pending.Provider, pending.Subject, now);
        if (linkResult.IsError)
        {
            return linkResult.Errors;
        }

        var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return UsersErrors.ExternalLoginLinkedToOtherUser;
        }

        await bus.PublishAsync(new ExternalLoginLinkedV1(user.Id.Value, user.Email.Value, pending.Provider.ToString(), pending.Subject, now, Guid.NewGuid()));
        await bus.PublishAsync(new UserLoggedInV1(user.Id.Value, user.Email.Value, cmd.IpAddress ?? string.Empty));
        UsersTelemetry.EventsPublished.Add(2, new KeyValuePair<string, object?>("event", "ExternalLoginLinkedV1+UserLoggedInV1"));

        var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, user.Role.Name, refreshToken.Id.Value);

        return new GoogleLoginConfirmResponse(
            user.Id.Value,
            accessToken,
            now.AddMinutes(opts.AccessTokenLifetimeMinutes),
            rawRefreshToken,
            refreshToken.ExpiresAt,
            IsNewUser: false);
    }

    private async Task<ErrorOr<GoogleLoginConfirmResponse>> ProvisionNewUserAsync(
        Email email,
        PendingExternalLogin pending,
        GoogleLoginConfirmCommand cmd,
        UsersOptions opts,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var userResult = User.CreateExternal(email, pending.DisplayName, pending.Provider, pending.Subject, clock);
        if (userResult.IsError)
        {
            return userResult.Errors;
        }

        var user = userResult.Value;

        var linkResult = user.LinkExternalLogin(pending.Provider, pending.Subject, now);
        if (linkResult.IsError)
        {
            return linkResult.Errors;
        }

        db.Users.Add(user);
        var consent = Consent.Grant(user.Id.Value, ConsentKeys.WelcomeEmail, now, cmd.IpAddress, cmd.UserAgent);
        db.Consents.Add(consent);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                                           string.Equals(pg.SqlState, "23505", StringComparison.Ordinal))
        {
            if (string.Equals(pg.ConstraintName, "ix_external_logins_provider_subject", StringComparison.Ordinal))
            {
                return UsersErrors.ExternalLoginLinkedToOtherUser;
            }

            if (string.Equals(pg.ConstraintName, "ix_users_email", StringComparison.Ordinal))
            {
                // Registration committed after our existing-user lookup but before this insert.
                // Roll the failed graph out of the DbContext and retry through the link path.
                return await RetryLinkAfterConcurrentRegistrationAsync(user, consent, email, pending, cmd, opts, now, ct);
            }

            return UsersErrors.EmailAlreadyRegistered;
        }

        var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new UserProvisionedFromExternalV1(
            user.Id.Value, pending.Provider.ToString(), pending.Subject,
            user.Email.Value, user.DisplayName, now, Guid.NewGuid()));
        await bus.PublishAsync(new UserLoggedInV1(user.Id.Value, user.Email.Value, cmd.IpAddress ?? string.Empty));
        UsersTelemetry.EventsPublished.Add(2, new KeyValuePair<string, object?>("event", "UserProvisionedFromExternalV1+UserLoggedInV1"));

        var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, user.Role.Name, refreshToken.Id.Value);

        return new GoogleLoginConfirmResponse(
            user.Id.Value,
            accessToken,
            now.AddMinutes(opts.AccessTokenLifetimeMinutes),
            rawRefreshToken,
            refreshToken.ExpiresAt,
            IsNewUser: true);
    }

    private async Task<ErrorOr<GoogleLoginConfirmResponse>> RetryLinkAfterConcurrentRegistrationAsync(
        User provisionedUser,
        Consent consent,
        Email email,
        PendingExternalLogin pending,
        GoogleLoginConfirmCommand cmd,
        UsersOptions opts,
        DateTimeOffset now,
        CancellationToken ct)
    {
        DetachFailedProvisioningAttempt(provisionedUser, consent);

        var existingUser = await LoadUserForEmailAsync(email, ct);
        if (existingUser is null)
        {
            return UsersErrors.EmailAlreadyRegistered;
        }

        return await LinkToExistingUserAsync(existingUser, pending, cmd, opts, now, ct);
    }

    private async Task<User?> LoadUserForEmailAsync(Email email, CancellationToken ct)
        => await db.Users
            .FromSqlInterpolated($"""
                SELECT id, created_at, created_by, display_name, email,
                       has_completed_onboarding, password_hash, role,
                       updated_at, updated_by, xmin
                FROM users.users
                WHERE email = {email.Value}
                FOR UPDATE
                """)
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(ct);

    private void DetachFailedProvisioningAttempt(User user, Consent consent)
    {
        var logins = user.ExternalLogins.ToArray();

        foreach (var login in logins)
        {
            var loginEntry = db.Entry(login);
            if (loginEntry.State != EntityState.Detached)
            {
                loginEntry.State = EntityState.Detached;
            }
        }

        var consentEntry = db.Entry(consent);
        if (consentEntry.State != EntityState.Detached)
        {
            consentEntry.State = EntityState.Detached;
        }

        var userEntry = db.Entry(user);
        if (userEntry.State != EntityState.Detached)
        {
            userEntry.State = EntityState.Detached;
        }
    }
}
