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

    // Avatar
    public static readonly Error AvatarMissing =
        Error.NotFound("Users.Avatar.NotFound", "Avatar was not found.");

    public static readonly Error AvatarTooLarge =
        Error.Validation("Users.Avatar.TooLarge", "Avatar image cannot exceed 1 MB.");

    public static readonly Error AvatarContentTypeUnsupported =
        Error.Validation("Users.Avatar.ContentTypeUnsupported", "Avatar image must be JPEG, PNG, or WebP.");

    public static readonly Error AvatarInvalid =
        Error.Validation("Users.Avatar.Invalid", "Avatar image is invalid or could not be decoded.");

    public static readonly Error AvatarDimensionsInvalid =
        Error.Validation("Users.Avatar.DimensionsInvalid", "Avatar image must be square and between 128x128 and 512x512 pixels.");

    public static readonly Error AvatarAccessDenied =
        Error.Forbidden("Users.Avatar.AccessDenied", "You are not allowed to access this avatar.");

    // Registration and authentication
    public static readonly Error EmailAlreadyRegistered =
        Error.Conflict("Users.EmailAlreadyRegistered", "An account with this email address already exists.");

    public static readonly Error RegistrationUnavailable =
        Error.Validation("Users.Registration.Unavailable", "Registration is not available for this account.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Users.InvalidCredentials", "The email address or password is incorrect.");

    public static readonly Error EmailNotConfirmed =
        Error.Unauthorized("Users.Email.NotConfirmed", "Please confirm your email address before signing in.");

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

    public static readonly Error InvitationLifetimeInvalid =
        Error.Validation("Users.Invitation.LifetimeInvalid", "Invitation lifetime must be greater than zero.");

    public static readonly Error InvitationInvalid =
        Error.Validation("Users.Invitation.Invalid", "The invitation is invalid, expired, or has already been used.");

    public static readonly Error InvitationAlreadyExists =
        Error.Conflict("Users.Invitation.AlreadyExists", "An active invitation already exists for this email address.");

    public static readonly Error InvitationAlreadyAccepted =
        Error.Conflict("Users.Invitation.AlreadyAccepted", "The invitation has already been accepted.");

    public static readonly Error InvitationAlreadyRevoked =
        Error.Conflict("Users.Invitation.AlreadyRevoked", "The invitation has already been revoked.");

    public static readonly Error InvitationStatusInvalid =
        Error.Validation("Users.Invitation.StatusInvalid", "Invitation status must be pending, expired, revoked, accepted, or all.");

    public static readonly Error OnboardingRequired =
        Error.Unauthorized("Users.Onboarding.Required", "Account setup is not complete. Please complete onboarding first.");

    public static readonly Error TermsNotAccepted =
        Error.Validation("Users.Onboarding.TermsNotAccepted", "Terms of service must be accepted.");

    public static readonly Error LegalDocumentsUnavailable =
        Error.Validation("Users.Onboarding.LegalDocumentsUnavailable", "Required legal documents are not available.");

    public static readonly Error RequiredLegalDocumentMissing =
        Error.Validation("Users.Onboarding.RequiredLegalDocumentMissing", "All required legal documents must be accepted.");

    public static readonly Error LegalDocumentAcceptanceInvalid =
        Error.Validation("Users.Onboarding.LegalDocumentAcceptanceInvalid", "One or more accepted legal documents are stale or invalid.");

    // Two-factor authentication
    public static readonly Error TwoFactorRequired =
        Error.Unauthorized("Users.TwoFactor.Required", "Two-factor authentication is required to complete sign-in.");

    public static readonly Error TwoFactorAlreadyEnabled =
        Error.Conflict("Users.TwoFactor.AlreadyEnabled", "Two-factor authentication is already enabled.");

    public static readonly Error TwoFactorNotEnabled =
        Error.Conflict("Users.TwoFactor.NotEnabled", "Two-factor authentication is not enabled.");

    public static readonly Error TwoFactorSetupNotFound =
        Error.NotFound("Users.TwoFactor.SetupNotFound", "Two-factor setup was not found.");

    public static readonly Error TwoFactorSecretInvalid =
        Error.Validation("Users.TwoFactor.SecretInvalid", "The two-factor secret is invalid.");

    public static readonly Error TwoFactorCodeInvalid =
        Error.Validation("Users.TwoFactor.CodeInvalid", "The two-factor code is invalid.");

    public static readonly Error RecoveryCodeInvalid =
        Error.Validation("Users.TwoFactor.RecoveryCodeInvalid", "The recovery code is invalid.");

    // Pagination
    public static readonly Error PageInvalid =
        Error.Validation("Users.Query.PageInvalid", $"Page number must be between 1 and {Shared.Kernel.Pagination.PageRequest.MaxPage}.");

    public static readonly Error PageSizeInvalid =
        Error.Validation("Users.Query.PageSizeInvalid", $"Page size must be between 1 and {Shared.Kernel.Pagination.PageRequest.MaxPageSize}.");
}
