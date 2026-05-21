namespace Modulith.Modules.Organizations.Errors;

internal static class OrganizationsErrors
{
    public static readonly ErrorOr.Error OrganizationNotFound =
        ErrorOr.Error.NotFound("Organizations.Organization.NotFound", "Organization was not found.");

    public static readonly ErrorOr.Error OrganizationDeleted =
        ErrorOr.Error.Conflict("Organizations.Organization.Deleted", "Organization is deleted.");

    public static readonly ErrorOr.Error NameEmpty =
        ErrorOr.Error.Validation("Organizations.Name.Empty", "Organization name cannot be empty.");

    public static readonly ErrorOr.Error NameTooLong =
        ErrorOr.Error.Validation("Organizations.Name.TooLong", "Organization name cannot exceed 200 characters.");

    public static readonly ErrorOr.Error SlugEmpty =
        ErrorOr.Error.Validation("Organizations.Slug.Empty", "Organization slug cannot be empty.");

    public static readonly ErrorOr.Error SlugInvalid =
        ErrorOr.Error.Validation("Organizations.Slug.Invalid", "Organization slug must contain lowercase letters, numbers, and hyphens only.");

    public static readonly ErrorOr.Error SlugTooLong =
        ErrorOr.Error.Validation("Organizations.Slug.TooLong", "Organization slug cannot exceed 100 characters.");

    public static readonly ErrorOr.Error SlugAlreadyExists =
        ErrorOr.Error.Conflict("Organizations.Slug.AlreadyExists", "Organization slug is already in use.");

    public static readonly ErrorOr.Error MemberAlreadyExists =
        ErrorOr.Error.Conflict("Organizations.Member.AlreadyExists", "User is already a member of this organization.");

    public static readonly ErrorOr.Error MemberNotFound =
        ErrorOr.Error.NotFound("Organizations.Member.NotFound", "Organization member was not found.");

    public static readonly ErrorOr.Error LastOwnerRequired =
        ErrorOr.Error.Conflict("Organizations.Owner.LastOwnerRequired", "An active organization must have at least one owner.");

    public static readonly ErrorOr.Error OwnedOrganizationsBlockUserErasure =
        ErrorOr.Error.Conflict("Organizations.Owner.UserErasureBlocked", "Transfer ownership or delete owned organizations before deleting this user.");

    public static readonly ErrorOr.Error RoleInvalid =
        ErrorOr.Error.Validation("Organizations.Role.Invalid", "Organization role is not valid.");

    public static readonly ErrorOr.Error InvitationInvalid =
        ErrorOr.Error.Validation("Organizations.Invitation.Invalid", "Organization invitation is invalid.");

    public static readonly ErrorOr.Error InvitationAlreadyAccepted =
        ErrorOr.Error.Conflict("Organizations.Invitation.AlreadyAccepted", "Organization invitation has already been accepted.");

    public static readonly ErrorOr.Error InvitationAlreadyRevoked =
        ErrorOr.Error.Conflict("Organizations.Invitation.AlreadyRevoked", "Organization invitation has already been revoked.");

    public static readonly ErrorOr.Error InvitationLifetimeInvalid =
        ErrorOr.Error.Validation("Organizations.Invitation.LifetimeInvalid", "Organization invitation lifetime must be greater than zero.");
}
