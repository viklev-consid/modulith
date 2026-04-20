using ErrorOr;

namespace Modulith.Modules.Users.Errors;

internal static class UsersErrors
{
    public static readonly Error EmailAlreadyRegistered =
        Error.Conflict("Users.EmailAlreadyRegistered", "An account with this email address already exists.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Users.InvalidCredentials", "The email address or password is incorrect.");

    public static readonly Error UserNotFound =
        Error.NotFound("Users.UserNotFound", "User was not found.");
}
