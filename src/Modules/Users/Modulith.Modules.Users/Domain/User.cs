using ErrorOr;
using Modulith.Modules.Users.Domain.Events;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class User : AggregateRoot<UserId>, IAuditableEntity
{
    private readonly List<ExternalLogin> _externalLogins = [];

    private User(
        UserId id,
        Email email,
        PasswordHash? passwordHash,
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

    /// <summary>Null for external-only users who have not yet set a password.</summary>
    public PasswordHash? PasswordHash { get; private set; }

    public string DisplayName { get; private set; } = null!;
    public Role Role { get; private set; } = Role.User;
    public bool HasCompletedOnboarding { get; private set; }

    public IReadOnlyList<ExternalLogin> ExternalLogins => _externalLogins.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>
    /// Creates a user registered with email and password.
    /// HasCompletedOnboarding is true — the registration form is the onboarding step.
    /// Raises UserRegistered.
    /// </summary>
    public static ErrorOr<User> CreateWithPassword(
        Email email,
        PasswordHash passwordHash,
        string displayName,
        Role? initialRole = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return UsersErrors.DisplayNameEmpty;
        }

        if (displayName.Length > 100)
        {
            return UsersErrors.DisplayNameTooLong;
        }

        var role = initialRole ?? Role.User;
        var user = new User(UserId.New(), email, passwordHash, displayName.Trim(), role, hasCompletedOnboarding: true);
        user.RaiseEvent(new UserRegistered(user.Id, email.Value, displayName.Trim()));
        return user;
    }

    /// <summary>
    /// Creates a user provisioned from an external login (e.g. Google).
    /// HasCompletedOnboarding is false — the client must call CompleteOnboarding.
    /// Raises UserProvisionedFromExternal (not UserRegistered).
    /// </summary>
    public static ErrorOr<User> CreateExternal(
        Email email,
        string displayName,
        ExternalLoginProvider provider,
        string subject,
        IClock clock,
        Role? initialRole = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return UsersErrors.DisplayNameEmpty;
        }

        if (displayName.Length > 100)
        {
            return UsersErrors.DisplayNameTooLong;
        }

        var role = initialRole ?? Role.User;
        var user = new User(UserId.New(), email, passwordHash: null, displayName.Trim(), role, hasCompletedOnboarding: false);
        var now = clock.UtcNow;
        user.RaiseEvent(new UserProvisionedFromExternal(user.Id, provider, subject, email.Value, displayName.Trim(), now));
        return user;
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

    /// <summary>
    /// Updates the password. Used by ChangePassword and ResetPassword flows.
    /// For external-only users setting their first password, use SetInitialPassword instead.
    /// </summary>
    public ErrorOr<Success> SetPassword(PasswordHash newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        RaiseEvent(new UserPasswordChanged(Id));
        return Result.Success;
    }

    /// <summary>
    /// Sets the first password for an external-only user.
    /// Fails if the user already has a password — use ChangePassword / SetPassword instead.
    /// </summary>
    public ErrorOr<Success> SetInitialPassword(PasswordHash passwordHash)
    {
        if (PasswordHash is not null)
        {
            return UsersErrors.PasswordAlreadySet;
        }

        PasswordHash = passwordHash;
        RaiseEvent(new UserPasswordChanged(Id));
        return Result.Success;
    }

    /// <summary>
    /// Links an external provider identity to this user.
    /// Fails if any credential for this provider is already linked to this user —
    /// the model enforces at most one external login per provider per user.
    /// </summary>
    public ErrorOr<Success> LinkExternalLogin(
        ExternalLoginProvider provider,
        string subject,
        DateTimeOffset linkedAt)
    {
        if (_externalLogins.Any(e => e.Provider == provider))
        {
            return UsersErrors.ExternalLoginAlreadyLinked;
        }

        var login = ExternalLogin.Create(Id, provider, subject, linkedAt);
        _externalLogins.Add(login);
        RaiseEvent(new ExternalLoginLinked(Id, provider, subject, linkedAt));
        return Result.Success;
    }

    /// <summary>
    /// Unlinks an external provider identity from this user.
    /// Enforces the credential-retention guardrail: the user must always retain at
    /// least one credential (password or another external login).
    /// </summary>
    public ErrorOr<Success> UnlinkExternalLogin(ExternalLoginProvider provider)
    {
        var login = _externalLogins.FirstOrDefault(e => e.Provider == provider);
        if (login is null)
        {
            return UsersErrors.ExternalLoginNotLinked;
        }

        var hasOtherCredential = PasswordHash is not null || _externalLogins.Count > 1;
        if (!hasOtherCredential)
        {
            return UsersErrors.CredentialRetentionViolation;
        }

        _externalLogins.Remove(login);
        RaiseEvent(new ExternalLoginUnlinked(Id, provider));
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
