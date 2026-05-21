using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Modulith.Shared.Infrastructure.Frontend;

public sealed class FrontendUrlBuilder(IOptions<FrontendOptions> options) : IFrontendUrlBuilder
{
    public string ConfirmEmail(string token) =>
        Build(options.Value.Paths.ConfirmEmail, new Dictionary<string, string?>
        (StringComparer.Ordinal)
        {
            ["token"] = token,
        });

    public string ConfirmEmailChange(string token) =>
        Build(options.Value.Paths.ConfirmEmailChange, new Dictionary<string, string?>
        (StringComparer.Ordinal)
        {
            ["token"] = token,
        });

    public string ResetPassword(string token) =>
        Build(options.Value.Paths.ResetPassword, new Dictionary<string, string?>
        (StringComparer.Ordinal)
        {
            ["token"] = token,
        });

    public string AcceptUserInvitation(string token, string email) =>
        Build(options.Value.Paths.UserInvitation, new Dictionary<string, string?>
        (StringComparer.Ordinal)
        {
            ["token"] = token,
            ["email"] = email,
            ["lockEmail"] = "1",
        });

    public string AcceptOrganizationInvitation(string token, string email) =>
        Build(options.Value.Paths.OrganizationInvitation, new Dictionary<string, string?>
        (StringComparer.Ordinal)
        {
            ["token"] = token,
            ["email"] = email,
        });

    private string Build(string path, IDictionary<string, string?> query)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        return QueryHelpers.AddQueryString($"{baseUrl}{path}", query);
    }
}
