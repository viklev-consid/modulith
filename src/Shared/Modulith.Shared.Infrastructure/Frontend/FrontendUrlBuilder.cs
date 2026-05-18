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

    public string ConfirmGoogleLogin(string token) =>
        Build(options.Value.Paths.GoogleConfirm, new Dictionary<string, string?>
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

    private string Build(string path, IDictionary<string, string?> query)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        return QueryHelpers.AddQueryString($"{baseUrl}{path}", query);
    }
}
