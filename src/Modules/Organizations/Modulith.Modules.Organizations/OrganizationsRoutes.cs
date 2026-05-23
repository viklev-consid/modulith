namespace Modulith.Modules.Organizations;

internal static class OrganizationsRoutes
{
    public const string GroupTag = "Organizations";
    public const string Prefix = "/v1/organizations";
    public const string MyOrganizations = $"{Prefix}/my";
    public const string ByRef = $"{Prefix}/{{organizationRef}}";
    public const string Members = $"{ByRef}/members";
    public const string MemberByUserId = $"{Members}/{{userId:guid}}";
    public const string MemberRole = $"{MemberByUserId}/role";
    public const string Invitations = $"{ByRef}/invitations";
    public const string InvitationById = $"{Invitations}/{{invitationId:guid}}";
    public const string AcceptInvitation = $"{Prefix}/invitations/accept";
    public const string Audit = $"{ByRef}/audit";
}
