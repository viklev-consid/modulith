using ErrorOr;
using Modulith.Modules.Users.Domain.Events;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class User : AggregateRoot<UserId>, IAuditableEntity
{
    private User(UserId id, Email email, PasswordHash passwordHash, string displayName)
        : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName;
    }

    // Required by EF Core for materialization.
    private User() : base(default!) { }

    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static ErrorOr<User> Create(Email email, PasswordHash passwordHash, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return Error.Validation("Users.DisplayName.Empty", "Display name cannot be empty.");

        if (displayName.Length > 100)
            return Error.Validation("Users.DisplayName.TooLong", "Display name cannot exceed 100 characters.");

        var user = new User(UserId.New(), email, passwordHash, displayName.Trim());
        user.RaiseEvent(new UserRegistered(user.Id, email.Value, displayName.Trim()));
        return user;
    }

    public ErrorOr<Success> ChangeEmail(Email newEmail)
    {
        if (Email == newEmail)
            return Error.Conflict("Users.Email.Same", "The new email address is the same as the current one.");

        var oldEmail = Email;
        Email = newEmail;
        RaiseEvent(new UserEmailChanged(Id, oldEmail.Value, newEmail.Value));
        return Result.Success;
    }

    public ErrorOr<Success> SetPassword(PasswordHash newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        RaiseEvent(new UserPasswordChanged(Id));
        return Result.Success;
    }
}
