using Microsoft.Extensions.Options;

namespace Modulith.Shared.Infrastructure.Frontend;

public sealed class FrontendOptionsValidator : IValidateOptions<FrontendOptions>
{
    public ValidateOptionsResult Validate(string? name, FrontendOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https"))
        {
            failures.Add("Frontend:BaseUrl must be an absolute HTTP(S) URL.");
        }
        else if (string.Equals(baseUri.Host, "app.example.com", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Frontend:BaseUrl must be configured for this environment, not the app.example.com placeholder.");
        }

        ValidatePath(options.Paths.ConfirmEmail, "Frontend:Paths:ConfirmEmail", failures);
        ValidatePath(options.Paths.ConfirmEmailChange, "Frontend:Paths:ConfirmEmailChange", failures);
        ValidatePath(options.Paths.GoogleConfirm, "Frontend:Paths:GoogleConfirm", failures);
        ValidatePath(options.Paths.ResetPassword, "Frontend:Paths:ResetPassword", failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidatePath(string path, string key, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            failures.Add($"{key} is required.");
            return;
        }

        if (path[0] != '/')
        {
            failures.Add($"{key} must be a rooted relative path beginning with '/'.");
        }

        if (path.StartsWith("//", StringComparison.Ordinal) ||
            (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
        {
            failures.Add($"{key} must not be a full URL.");
        }

        if (path.Contains('?') ||
            path.Contains('#'))
        {
            failures.Add($"{key} must not include query strings or fragments.");
        }
    }
}
