using ErrorOr;
using Modulith.Modules.Users.Errors;

namespace Modulith.Modules.Users.Domain;

/// <summary>
/// Represents a named user role. Roles are case-sensitive ASCII identifiers matching
/// <c>^[a-z][a-z0-9_-]{1,31}$</c>.  Only two roles ship by default: <see cref="Admin"/>
/// and <see cref="User"/>.  Additional roles can be defined without schema changes.
/// </summary>
public sealed record Role(string Name)
{
    private static readonly System.Text.RegularExpressions.Regex ValidPattern =
        new(@"^[a-z][a-z0-9_-]{1,31}$",
            System.Text.RegularExpressions.RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

    /// <summary>Seeded role that holds every declared permission across all modules.</summary>
    public static readonly Role Admin = new("admin");

    /// <summary>Seeded role that holds no elevated permissions.</summary>
    public static readonly Role User = new("user");

    public static ErrorOr<Role> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return UsersErrors.RoleNameEmpty;
        }

        if (!ValidPattern.IsMatch(name))
        {
            return UsersErrors.RoleNameInvalid;
        }

        return new Role(name);
    }

    public override string ToString() => Name;
}
