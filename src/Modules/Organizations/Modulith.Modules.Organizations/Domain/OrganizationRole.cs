using ErrorOr;
using Modulith.Modules.Organizations.Errors;

namespace Modulith.Modules.Organizations.Domain;

public sealed record OrganizationRole
{
    public static readonly OrganizationRole Owner = new("owner");
    public static readonly OrganizationRole Admin = new("admin");
    public static readonly OrganizationRole Member = new("member");
    public static readonly OrganizationRole Viewer = new("viewer");

    private static readonly Dictionary<string, OrganizationRole> known =
        new Dictionary<string, OrganizationRole>(StringComparer.OrdinalIgnoreCase)
        {
            [Owner.Name] = Owner,
            [Admin.Name] = Admin,
            [Member.Name] = Member,
            [Viewer.Name] = Viewer
        };

    private OrganizationRole(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public static IReadOnlyCollection<OrganizationRole> All { get; } =
        [Owner, Admin, Member, Viewer];

    public static ErrorOr<OrganizationRole> Create(string name)
    {
        if (known.TryGetValue(name.Trim(), out var role))
        {
            return role;
        }

        return OrganizationsErrors.RoleInvalid;
    }

    public override string ToString() => Name;
}
