using ErrorOr;

namespace Modulith.Modules.Users.Errors;

internal static class UsersErrors
{
    // Email value object
    public static readonly Error EmailEmpty =
        Error.Validation("Users.Email.Empty", "Email address cannot be empty.");

    public static readonly Error EmailTooLong =
        Error.Validation("Users.Email.TooLong", "Email address cannot exceed 254 characters.");

    public static readonly Error EmailInvalid =
        Error.Validation("Users.Email.Invalid", "Email address format is invalid.");

    public static readonly Error EmailSame =
        Error.Conflict("Users.Email.Same", "The new email address is the same as the current one.");

    // User aggregate
    public static readonly Error DisplayNameEmpty =
        Error.Validation("Users.DisplayName.Empty", "Display name cannot be empty.");

    public static readonly Error DisplayNameTooLong =
        Error.Validation("Users.DisplayName.TooLong", "Display name cannot exceed 100 characters.");

    // Registration and authentication
    public static readonly Error EmailAlreadyRegistered =
        Error.Conflict("Users.EmailAlreadyRegistered", "An account with this email address already exists.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Users.InvalidCredentials", "The email address or password is incorrect.");

    public static readonly Error UserNotFound =
        Error.NotFound("Users.UserNotFound", "User was not found.");
}
