using ErrorOr;

namespace Modulith.Modules.Users.Domain;

public sealed record Email(string Value)
{
    public static ErrorOr<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error.Validation("Users.Email.Empty", "Email address cannot be empty.");

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 254)
            return Error.Validation("Users.Email.TooLong", "Email address cannot exceed 254 characters.");

        var atIndex = normalized.IndexOf('@');
        if (atIndex <= 0 || atIndex == normalized.Length - 1)
            return Error.Validation("Users.Email.Invalid", "Email address format is invalid.");

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
