namespace Modulith.Shared.Infrastructure.Frontend;

public interface IFrontendUrlBuilder
{
    string ConfirmEmail(string token);

    string ConfirmEmailChange(string token);

    string ResetPassword(string token);

    string AcceptUserInvitation(string token);

    string AcceptOrganizationInvitation(string token);
}
