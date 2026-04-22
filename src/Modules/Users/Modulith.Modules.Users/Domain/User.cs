using ErrorOr;
using Modulith.Modules.Users.Domain.Events;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class User : AggregateRoot<UserId>, IAuditableEntity
{
    private User(UserId id, Email email, PasswordHash passwordHash, string displayName, Role role)
        : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName;
        Role = role;
    }

    // Required by EF Core for materialization.
    private User() : base(default!) { }

    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public Role Role { get; private set; } = Role.User;

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static ErrorOr<User> Create(
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
        var user = new User(UserId.New(), email, passwordHash, displayName.Trim(), role);
        user.RaiseEvent(new UserRegistered(user.Id, email.Value, displayName.Trim()));
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

    public ErrorOr<Success> SetPassword(PasswordHash newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        RaiseEvent(new UserPasswordChanged(Id));
        return Result.Success;
    }
}
