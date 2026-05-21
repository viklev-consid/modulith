using ErrorOr;
using Modulith.Modules.Organizations.Errors;

namespace Modulith.Modules.Organizations.Domain;

public sealed record OrganizationRole
{
    public static readonly OrganizationRole Owner = new("owner", rank: 3);
    public static readonly OrganizationRole Admin = new("admin", rank: 2);
    public static readonly OrganizationRole Member = new("member", rank: 1);
    public static readonly OrganizationRole Viewer = new("viewer", rank: 0);

    private static readonly Dictionary<string, OrganizationRole> known =
        new Dictionary<string, OrganizationRole>(StringComparer.OrdinalIgnoreCase)
        {
            [Owner.Name] = Owner,
            [Admin.Name] = Admin,
            [Member.Name] = Member,
            [Viewer.Name] = Viewer
        };

    private OrganizationRole(string name, int rank)
    {
        Name = name;
        Rank = rank;
    }

    public string Name { get; }
    public int Rank { get; }

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
