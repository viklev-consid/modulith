namespace Modulith.Modules.Users;

internal static class UsersRoutes
{
    public const string GroupTag = "Users";
    public const string Prefix = "/v1/users";
    public const string Register = $"{Prefix}/register";
    public const string Login = $"{Prefix}/login";
    public const string Me = $"{Prefix}/me";
    public const string PersonalData = $"{Me}/personal-data";
}
