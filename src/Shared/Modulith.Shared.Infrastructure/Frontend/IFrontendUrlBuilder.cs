namespace Modulith.Shared.Infrastructure.Frontend;

public interface IFrontendUrlBuilder
{
    string ConfirmEmailChange(string token);

    string ConfirmGoogleLogin(string token);

    string ResetPassword(string token);
}
