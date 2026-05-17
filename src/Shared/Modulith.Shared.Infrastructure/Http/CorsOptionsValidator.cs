using Microsoft.Extensions.Options;

namespace Modulith.Shared.Infrastructure.Http;

public sealed class CorsOptionsValidator : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.PolicyName))
        {
            failures.Add("Cors:PolicyName is required.");
        }

        if (options.AllowCredentials &&
            options.AllowedOrigins.Any(origin => string.Equals(origin, "*", StringComparison.Ordinal)))
        {
            failures.Add("Cors:AllowedOrigins cannot contain '*' when Cors:AllowCredentials is true.");
        }

        foreach (var origin in options.AllowedOrigins.Where(origin => !IsHttpOrigin(origin)))
        {
            failures.Add($"Cors:AllowedOrigins value '{origin}' must be an absolute HTTP(S) origin with no path, query, or fragment.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsHttpOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (string.Equals(uri.Host, "app.example.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.Ordinal);
    }
}
