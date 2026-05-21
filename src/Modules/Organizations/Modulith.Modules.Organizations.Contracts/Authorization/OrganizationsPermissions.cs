namespace Modulith.Modules.Organizations.Contracts.Authorization;

public static class OrganizationsPermissions
{
    public const string OrganizationsRead = "organizations.organizations.read";
    public const string OrganizationsWrite = "organizations.organizations.write";
    public const string MembersRead = "organizations.members.read";
    public const string MembersManage = "organizations.members.manage";
    public const string InvitationsManage = "organizations.invitations.manage";
    public const string AuditRead = "organizations.audit.read";
    public const string PlatformOverride = "organizations.platform.override";

    public static IReadOnlyCollection<string> All { get; } =
        [
            OrganizationsRead,
            OrganizationsWrite,
            MembersRead,
            MembersManage,
            InvitationsManage,
            AuditRead,
            PlatformOverride
        ];
}
