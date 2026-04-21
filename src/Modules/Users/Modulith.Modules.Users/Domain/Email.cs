using ErrorOr;
using Modulith.Modules.Users.Errors;

namespace Modulith.Modules.Users.Domain;

public sealed record Email(string Value)
{
    public static ErrorOr<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UsersErrors.EmailEmpty;
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 254)
        {
            return UsersErrors.EmailTooLong;
        }

        var atIndex = normalized.IndexOf('@');
        if (atIndex <= 0 || atIndex == normalized.Length - 1)
        {
            return UsersErrors.EmailInvalid;
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
