namespace Modulith.Modules.Users.Contracts.Authorization;

public static class UsersPermissions
{
    public const string UsersRead  = "users.users.read";
    public const string UsersWrite = "users.users.write";
    public const string RolesWrite = "users.roles.write";

    public static IReadOnlyCollection<string> All { get; } =
        [UsersRead, UsersWrite, RolesWrite];
}
