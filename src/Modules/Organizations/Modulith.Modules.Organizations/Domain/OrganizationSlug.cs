using ErrorOr;
using Modulith.Modules.Organizations.Errors;

namespace Modulith.Modules.Organizations.Domain;

public sealed record OrganizationSlug
{
    private OrganizationSlug(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ErrorOr<OrganizationSlug> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrganizationsErrors.SlugEmpty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 100)
        {
            return OrganizationsErrors.SlugTooLong;
        }

        if (!IsValidSlug(normalized))
        {
            return OrganizationsErrors.SlugInvalid;
        }

        return new OrganizationSlug(normalized);
    }

    public static ErrorOr<OrganizationSlug> FromName(string name)
    {
        var chars = new List<char>(name.Length);
        var lastWasHyphen = true;
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                chars.Add(c);
                lastWasHyphen = false;
                continue;
            }

            if ((char.IsWhiteSpace(c) || c == '-') && !lastWasHyphen)
            {
                chars.Add('-');
                lastWasHyphen = true;
            }
        }

        if (chars.Count > 0 && chars[^1] == '-')
        {
            chars.RemoveAt(chars.Count - 1);
        }

        return Create(new string([.. chars]));
    }

    public override string ToString() => Value;

    private static bool IsValidSlug(string value)
    {
        if (value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var lastWasHyphen = false;
        foreach (var c in value)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                lastWasHyphen = false;
                continue;
            }

            if (c == '-' && !lastWasHyphen)
            {
                lastWasHyphen = true;
                continue;
            }

            return false;
        }

        return true;
    }
}
