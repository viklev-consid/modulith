namespace Modulith.Modules.Users;

internal static class UsersRoutes
{
    public const string GroupTag = "Users";
    public const string Prefix = "/v1/users";
    public const string Register = $"{Prefix}/register";
    public const string Login = $"{Prefix}/login";
    public const string Me = $"{Prefix}/me";
    public const string PersonalData = $"{Me}/personal-data";
    public const string List = Prefix;
    public const string ById = $"{Prefix}/{{userId:guid}}";
    public const string ChangeRole = $"{Prefix}/{{userId:guid}}/role";

    // Auth flows — Phase 9.5
    public const string ForgotPassword = $"{Prefix}/password/forgot";
    public const string ResetPassword = $"{Prefix}/password/reset";
    public const string ChangePassword = $"{Me}/password";
    public const string RequestEmailChange = $"{Me}/email/request";
    public const string ConfirmEmailChange = $"{Me}/email/confirm";
    public const string RefreshToken = $"{Prefix}/token/refresh";
    public const string Logout = $"{Prefix}/logout";
    public const string LogoutAll = $"{Prefix}/logout/all";
}
