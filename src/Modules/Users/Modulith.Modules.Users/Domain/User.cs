using ErrorOr;
using Modulith.Modules.Users.Domain.Events;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class User : AggregateRoot<UserId>, IAuditableEntity
{
    private User(
        UserId id,
        Email email,
        PasswordHash passwordHash,
        string displayName,
        Role role,
        bool hasCompletedOnboarding)
        : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName;
        Role = role;
        HasCompletedOnboarding = hasCompletedOnboarding;
    }

    // Required by EF Core for materialization.
    private User() : base(default!) { }

    public Email Email { get; private set; } = null!;

    public PasswordHash PasswordHash { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;
    public string? AvatarContainer { get; private set; }
    public string? AvatarKey { get; private set; }
    public string? AvatarContentType { get; private set; }
    public long? AvatarSizeBytes { get; private set; }
    public DateTimeOffset? AvatarUpdatedAt { get; private set; }
    public Role Role { get; private set; } = Role.User;
    public bool HasCompletedOnboarding { get; private set; }
    public DateTimeOffset? EmailConfirmedAt { get; private set; }
    public bool IsEmailConfirmed => EmailConfirmedAt is not null;
    public bool HasAvatar => AvatarContainer is not null && AvatarKey is not null;

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>
    /// Creates a user registered with email and password.
    /// HasCompletedOnboarding is false — the client must call CompleteOnboarding.
    /// Raises UserRegistered.
    /// </summary>
    public static ErrorOr<User> CreateWithPassword(
        Email email,
        PasswordHash passwordHash,
        string displayName,
        Role? initialRole = null)
    {
        var nameResult = NormalizeDisplayName(displayName);
        if (nameResult.IsError)
        {
            return nameResult.Errors;
        }

        var normalizedName = nameResult.Value;
        var role = initialRole ?? Role.User;
        var user = new User(UserId.New(), email, passwordHash, normalizedName, role, hasCompletedOnboarding: false);
        user.RaiseEvent(new UserRegistered(user.Id, email.Value, normalizedName));
        return user;
    }

    public bool ConfirmEmail(IClock clock)
    {
        if (EmailConfirmedAt is not null)
        {
            return false;
        }

        EmailConfirmedAt = clock.UtcNow;
        return true;
    }

    public ErrorOr<Success> ChangeRole(Role newRole, UserId changedBy)
    {
        if (Role == newRole)
        {
            return UsersErrors.RoleSame;
        }

        var oldRole = Role;
        Role = newRole;
        RaiseEvent(new UserRoleChanged(Id, oldRole.Name, newRole.Name, changedBy));
        return Result.Success;
    }

    public ErrorOr<Success> ChangeEmail(Email newEmail)
    {
        if (Email == newEmail)
        {
            return UsersErrors.EmailSame;
        }

        var oldEmail = Email;
        Email = newEmail;
        RaiseEvent(new UserEmailChanged(Id, oldEmail.Value, newEmail.Value));
        return Result.Success;
    }

    public ErrorOr<Success> UpdateProfile(string displayName)
    {
        var nameResult = NormalizeDisplayName(displayName);
        if (nameResult.IsError)
        {
            return nameResult.Errors;
        }

        var normalized = nameResult.Value;
        if (string.Equals(normalized, DisplayName, StringComparison.Ordinal))
        {
            return Result.Success;
        }

        var oldDisplayName = DisplayName;
        DisplayName = normalized;
        RaiseEvent(new UserProfileUpdated(Id, oldDisplayName, normalized));
        return Result.Success;
    }

    public (string? Container, string? Key) SetAvatar(
        string container,
        string key,
        string contentType,
        long sizeBytes,
        IClock clock)
    {
        var previous = (AvatarContainer, AvatarKey);
        AvatarContainer = container;
        AvatarKey = key;
        AvatarContentType = contentType;
        AvatarSizeBytes = sizeBytes;
        AvatarUpdatedAt = clock.UtcNow;
        return previous;
    }

    public (string? Container, string? Key) RemoveAvatar()
    {
        var previous = (AvatarContainer, AvatarKey);
        AvatarContainer = null;
        AvatarKey = null;
        AvatarContentType = null;
        AvatarSizeBytes = null;
        AvatarUpdatedAt = null;
        return previous;
    }

    private static ErrorOr<string> NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return UsersErrors.DisplayNameEmpty;
        }

        var trimmed = displayName.Trim();
        if (trimmed.Length > 100)
        {
            return UsersErrors.DisplayNameTooLong;
        }

        return trimmed;
    }

    public ErrorOr<Success> SetPassword(PasswordHash newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        RaiseEvent(new UserPasswordChanged(Id));
        return Result.Success;
    }

    /// <summary>
    /// Marks onboarding as complete. Idempotent — a no-op if already complete.
    /// </summary>
    public ErrorOr<Success> CompleteOnboarding()
    {
        if (!HasCompletedOnboarding)
        {
            HasCompletedOnboarding = true;
            RaiseEvent(new UserOnboardingCompleted(Id));
        }

        return Result.Success;
    }
}
