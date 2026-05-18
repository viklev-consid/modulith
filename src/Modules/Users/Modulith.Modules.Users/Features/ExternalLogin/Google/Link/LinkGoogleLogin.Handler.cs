using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Link;

public sealed class LinkGoogleLoginHandler(
    UsersDbContext db,
    IGoogleIdTokenVerifier verifier,
    IMessageBus bus,
    IGoogleAvatarImporter googleAvatarImporter,
    IUserAvatarStorage avatarStorage,
    IClock clock)
{
    public async Task<ErrorOr<Success>> Handle(LinkGoogleLoginCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(LinkGoogleLoginHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(LinkGoogleLoginCommand cmd, CancellationToken ct)
    {
        var identityResult = await verifier.VerifyAsync(cmd.IdToken, ct);
        if (identityResult.IsError)
        {
            return identityResult.Errors;
        }

        var identity = identityResult.Value;

        // Lock the user row so that two concurrent link requests for the same user cannot both
        // pass LinkExternalLogin's in-memory check and then race into SaveChanges.
        // The second waiter sees the ExternalLogin already committed and fails at the aggregate level.
        var user = await db.Users
            .FromSqlInterpolated($"""
                SELECT id, avatar_container, avatar_content_type, avatar_key, avatar_size_bytes, avatar_updated_at,
                       created_at, created_by, display_name, email,
                       email_confirmed_at, has_completed_onboarding, password_hash, role,
                       updated_at, updated_by, xmin
                FROM users.users
                WHERE id = {cmd.UserId}
                FOR UPDATE
                """)
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var now = clock.UtcNow;
        var linkResult = user.LinkExternalLogin(ExternalLoginProvider.Google, identity.Subject, identity.Email, now);
        if (linkResult.IsError)
        {
            return linkResult.Errors;
        }

        StoredAvatar? importedAvatar = null;
        (string? Container, string? Key) previousAvatar = default;
        if (cmd.OverrideAvatarWithGoogleAvatar)
        {
            importedAvatar = await googleAvatarImporter.ImportAsync(identity.PictureUrl, ct);
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

            DetachFailedLinkAttempt(user, identity.Subject);
            return ex.IsUniqueConstraintViolation("ix_external_logins_provider_subject")
                ? UsersErrors.ExternalLoginLinkedToOtherUser
                : UsersErrors.ExternalLoginAlreadyLinked;
        }

        if (importedAvatar is not null)
        {
            await avatarStorage.DeleteAsync(previousAvatar.Container, previousAvatar.Key, ct);
        }

        await bus.PublishAsync(new ExternalLoginLinkedV1(user.Id.Value, user.Email.Value, "Google", identity.Subject, now, Guid.NewGuid()));
        if (importedAvatar is not null)
        {
            await bus.PublishAsync(new UserAvatarUpdatedV1(user.Id.Value, Guid.NewGuid()));
            UsersTelemetry.EventsPublished.Add(2, new KeyValuePair<string, object?>("event", "ExternalLoginLinkedV1+UserAvatarUpdatedV1"));
        }
        else
        {
            UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(ExternalLoginLinkedV1)));
        }

        return Result.Success;
    }

    private void DetachFailedLinkAttempt(User user, string subject)
    {
        foreach (var entry in db.ChangeTracker.Entries<Domain.ExternalLogin>()
            .Where(e => e.State == EntityState.Added &&
                e.Entity.UserId == user.Id &&
                e.Entity.Provider == ExternalLoginProvider.Google &&
                string.Equals(e.Entity.Subject, subject, StringComparison.Ordinal))
            .ToList())
        {
            entry.State = EntityState.Detached;
        }

        // Drop the avatar mutation too; the link failed, so no profile side effect should commit.
        db.Entry(user).State = EntityState.Unchanged;
    }
}
