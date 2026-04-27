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
        Error.Unauthorized("Users.Password.CurrentIncorrect", "The current password is incorrect.");

    // Role errors
    public static readonly Error RoleNameEmpty =
        Error.Validation("Users.Role.Empty", "Role name cannot be empty.");

    public static readonly Error RoleNameInvalid =
        Error.Validation("Users.Role.Invalid",
            "Role name must match ^[a-z][a-z0-9_-]{1,31}$ (lowercase ASCII, no spaces).");

    public static readonly Error RoleSame =
        Error.Conflict("Users.Role.Same", "The user already has the specified role.");

    public static readonly Error RoleNotFound =
        Error.NotFound("Users.Role.NotFound", "The specified role does not exist.");

    public static readonly Error CannotChangeSelfRole =
        Error.Conflict("Users.Role.CannotChangeSelf", "An admin cannot change their own role.");

    public static readonly Error ConcurrencyConflict =
        Error.Conflict("Users.ConcurrencyConflict", "The user record was modified concurrently. Please retry.");

    // External login — Google verification
    public static readonly Error InvalidIdToken =
        Error.Unauthorized("Users.ExternalLogin.InvalidIdToken", "The identity token is invalid or could not be verified.");

    public static readonly Error ExternalAuthUnavailable =
        Error.Failure("Users.ExternalLogin.Unavailable", "External authentication is temporarily unavailable. Please try again later.");

    // External login errors
    public static readonly Error ExternalLoginAlreadyLinked =
        Error.Conflict("Users.ExternalLogin.AlreadyLinked", "This external account is already linked to your account.");

    public static readonly Error ExternalLoginNotLinked =
        Error.NotFound("Users.ExternalLogin.NotLinked", "This external account is not linked to your account.");

    public static readonly Error ExternalLoginLinkedToOtherUser =
        Error.Conflict("Users.ExternalLogin.LinkedToOtherUser", "This external account is already linked to a different user.");

    public static readonly Error CredentialRetentionViolation =
        Error.Conflict("Users.ExternalLogin.CredentialRetention", "Cannot unlink: you must retain at least one login credential (password or external account).");

    public static readonly Error PasswordAlreadySet =
        Error.Conflict("Users.Password.AlreadySet", "A password is already set. Use the change password flow instead.");

    public static readonly Error MaxPendingLoginsReached =
        Error.Conflict("Users.ExternalLogin.MaxPendingReached", "Too many pending confirmation attempts for this email address. Please try again later.");

    public static readonly Error OnboardingRequired =
        Error.Unauthorized("Users.Onboarding.Required", "Account setup is not complete. Please complete onboarding first.");

    public static readonly Error TermsNotAccepted =
        Error.Validation("Users.Onboarding.TermsNotAccepted", "Terms of service must be accepted.");

    // Pagination
    public static readonly Error PageInvalid =
        Error.Validation("Users.Query.PageInvalid", $"Page number must be between 1 and {Shared.Kernel.Pagination.PageRequest.MaxPage}.");

    public static readonly Error PageSizeInvalid =
        Error.Validation("Users.Query.PageSizeInvalid", $"Page size must be between 1 and {Shared.Kernel.Pagination.PageRequest.MaxPageSize}.");
}
