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

    // Token errors — single generic error to prevent token oracle attacks
    public static readonly Error InvalidOrExpiredToken =
        Error.Validation("Users.Token.InvalidOrExpired", "The token is invalid, expired, or has already been used.");

    // Refresh token errors
    public static readonly Error RefreshTokenNotFound =
        Error.Unauthorized("Users.RefreshToken.NotFound", "The refresh token is invalid.");

    public static readonly Error RefreshTokenExpired =
        Error.Unauthorized("Users.RefreshToken.Expired", "The refresh token has expired. Please log in again.");

    public static readonly Error RefreshTokenRevoked =
        Error.Unauthorized("Users.RefreshToken.Revoked", "The refresh token has been revoked.");

    // Password errors
    public static readonly Error CurrentPasswordIncorrect =
        Error.Validation("Users.Password.CurrentIncorrect", "The current password is incorrect.");
}
