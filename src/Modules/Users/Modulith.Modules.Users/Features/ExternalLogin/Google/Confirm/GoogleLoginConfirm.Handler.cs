using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;

public sealed class GoogleLoginConfirmHandler(
    UsersDbContext db,
    IJwtGenerator jwtGenerator,
    IRefreshTokenIssuer refreshTokenIssuer,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IGoogleAvatarImporter googleAvatarImporter,
    IUserAvatarStorage avatarStorage,
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

        UserInvitation? invitation = null;
        if (opts.Registration.Mode == RegistrationMode.Disabled)
        {
            return UsersErrors.RegistrationUnavailable;
        }

        if (opts.Registration.Mode == RegistrationMode.InviteOnly)
        {
            if (string.IsNullOrWhiteSpace(cmd.InvitationToken))
            {
                return UsersErrors.RegistrationUnavailable;
            }

            invitation = await LoadInvitationForTokenAsync(cmd.InvitationToken, ct);
            if (invitation is null)
            {
                return UsersErrors.RegistrationUnavailable;
            }
        }

        return await ProvisionNewUserAsync(email, pending, cmd, opts, now, invitation, ct);
    }

    private async Task<ErrorOr<GoogleLoginConfirmResponse>> LinkToExistingUserAsync(
        User user,
        PendingExternalLogin pending,
        GoogleLoginConfirmCommand cmd,
        UsersOptions opts,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var linkResult = user.LinkExternalLogin(pending.Provider, pending.Subject, pending.Email, now);
        if (linkResult.IsError)
        {
            return linkResult.Errors;
        }

        StoredAvatar? importedAvatar = null;
        (string? Container, string? Key) previousAvatar = default;
        if (cmd.UseGoogleAvatar)
        {
            importedAvatar = await googleAvatarImporter.ImportAsync(pending.ProviderAvatarUrl, ct);
            if (importedAvatar is not null)
            {
                previousAvatar = user.SetAvatar(
                    importedAvatar.BlobRef.Container,
                    importedAvatar.BlobRef.Key,
                    importedAvatar.ContentType,
                    importedAvatar.SizeBytes,
                    clock);
            }
        }

        var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            if (importedAvatar is not null)
            {
                await avatarStorage.DeleteAsync(importedAvatar.BlobRef.Container, importedAvatar.BlobRef.Key, ct);
            }

            DetachFailedExistingUserLinkAttempt(user, pending);
            return UsersErrors.ExternalLoginLinkedToOtherUser;
        }

        if (importedAvatar is not null)
        {
            await avatarStorage.DeleteAsync(previousAvatar.Container, previousAvatar.Key, ct);
        }

        await bus.PublishAsync(new ExternalLoginLinkedV1(user.Id.Value, user.Email.Value, pending.Provider.ToString(), pending.Subject, now, Guid.NewGuid()));
        if (importedAvatar is not null)
        {
            await bus.PublishAsync(new UserAvatarUpdatedV1(user.Id.Value, Guid.NewGuid()));
        }
        await bus.PublishAsync(new UserLoggedInV1(user.Id.Value, user.Email.Value, cmd.IpAddress ?? string.Empty, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(importedAvatar is null ? 2 : 3, new KeyValuePair<string, object?>("event", importedAvatar is null ? "ExternalLoginLinkedV1+UserLoggedInV1" : "ExternalLoginLinkedV1+UserAvatarUpdatedV1+UserLoggedInV1"));

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
        UserInvitation? invitation,
        CancellationToken ct)
    {
        var userResult = User.CreateExternal(email, pending.DisplayName, pending.Provider, pending.Subject, clock);
        if (userResult.IsError)
        {
            return userResult.Errors;
        }

        var user = userResult.Value;

        var linkResult = user.LinkExternalLogin(pending.Provider, pending.Subject, pending.Email, now);
        if (linkResult.IsError)
        {
            return linkResult.Errors;
        }

        StoredAvatar? importedAvatar = null;
        if (cmd.UseGoogleAvatar)
        {
            importedAvatar = await googleAvatarImporter.ImportAsync(pending.ProviderAvatarUrl, ct);
            if (importedAvatar is not null)
            {
                user.SetAvatar(
                    importedAvatar.BlobRef.Container,
                    importedAvatar.BlobRef.Key,
                    importedAvatar.ContentType,
                    importedAvatar.SizeBytes,
                    clock);
            }
        }

        if (invitation is not null)
        {
            var acceptResult = invitation.Accept(user.Id, email, clock);
            if (acceptResult.IsError)
            {
                return UsersErrors.RegistrationUnavailable;
            }
        }

        db.Users.Add(user);
        var consent = Consent.Grant(user.Id.Value, ConsentKeys.WelcomeEmail, now, cmd.IpAddress, cmd.UserAgent);
        db.Consents.Add(consent);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            if (importedAvatar is not null)
            {
                await avatarStorage.DeleteAsync(importedAvatar.BlobRef.Container, importedAvatar.BlobRef.Key, ct);
            }

            // Detach the failed provisioning graph before all return paths so AutoApplyTransactions'
            // SaveChangesAsync doesn't retry the failed entities.
            DetachFailedProvisioningAttempt(user, consent);

            if (ex.IsUniqueConstraintViolation("ix_external_logins_provider_subject"))
            {
                return UsersErrors.ExternalLoginLinkedToOtherUser;
            }

            if (ex.IsUniqueConstraintViolation("ix_users_email"))
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
        if (importedAvatar is not null)
        {
            await bus.PublishAsync(new UserAvatarUpdatedV1(user.Id.Value, Guid.NewGuid()));
        }
        await bus.PublishAsync(new UserLoggedInV1(user.Id.Value, user.Email.Value, cmd.IpAddress ?? string.Empty, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(importedAvatar is null ? 2 : 3, new KeyValuePair<string, object?>("event", importedAvatar is null ? "UserProvisionedFromExternalV1+UserLoggedInV1" : "UserProvisionedFromExternalV1+UserAvatarUpdatedV1+UserLoggedInV1"));

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
                SELECT id, avatar_container, avatar_content_type, avatar_key, avatar_size_bytes, avatar_updated_at,
                       created_at, created_by, display_name, email,
                       email_confirmed_at, has_completed_onboarding, password_hash, role,
                       updated_at, updated_by, xmin
                FROM users.users
                WHERE email = {email.Value}
                FOR UPDATE
                """)
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(ct);

    private void DetachFailedExistingUserLinkAttempt(User user, PendingExternalLogin pending)
    {
        foreach (var entry in db.ChangeTracker.Entries<Domain.ExternalLogin>()
            .Where(e => e.State == EntityState.Added &&
                e.Entity.UserId == user.Id &&
                e.Entity.Provider == pending.Provider &&
                string.Equals(e.Entity.Subject, pending.Subject, StringComparison.Ordinal))
            .ToList())
        {
            entry.State = EntityState.Detached;
        }

        // Drop the avatar mutation too; the link failed, so no profile side effect should commit.
        db.Entry(user).State = EntityState.Unchanged;
    }

    private async Task<UserInvitation?> LoadInvitationForTokenAsync(string rawToken, CancellationToken ct)
    {
        var tokenHash = UserInvitation.HashRawValue(rawToken);

        return await db.UserInvitations
            .FromSqlInterpolated($"""
                SELECT * FROM users.user_invitations
                WHERE token_hash = {tokenHash}
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(ct);
    }

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
