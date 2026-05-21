using Modulith.Modules.Organizations.Contracts.Authorization;
using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Authorization;

internal static class OrganizationRolePermissionMap
{
    private static readonly Dictionary<string, IReadOnlyCollection<string>> permissions =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [OrganizationRole.Owner.Name] =
            [
                OrganizationsPermissions.OrganizationsRead,
                OrganizationsPermissions.OrganizationsWrite,
                OrganizationsPermissions.MembersRead,
                OrganizationsPermissions.MembersManage,
                OrganizationsPermissions.InvitationsManage,
                OrganizationsPermissions.AuditRead
            ],
            [OrganizationRole.Admin.Name] =
            [
                OrganizationsPermissions.OrganizationsRead,
                OrganizationsPermissions.OrganizationsWrite,
                OrganizationsPermissions.MembersRead,
                OrganizationsPermissions.MembersManage,
                OrganizationsPermissions.InvitationsManage,
                OrganizationsPermissions.AuditRead
            ],
            [OrganizationRole.Member.Name] =
            [
                OrganizationsPermissions.OrganizationsRead,
                OrganizationsPermissions.MembersRead
            ],
            [OrganizationRole.Viewer.Name] =
            [
                OrganizationsPermissions.OrganizationsRead
            ]
        };

    public static IReadOnlyCollection<string> GetPermissions(OrganizationRole role) =>
        permissions.TryGetValue(role.Name, out var rolePermissions) ? rolePermissions : [];

    public static string GetVersion(OrganizationRole role)
    {
        var joined = string.Join('\n', GetPermissions(role).Order(StringComparer.Ordinal));
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(joined));
        return Convert.ToBase64String(hash)[..16].Replace('+', '-').Replace('/', '_');
    }
}
