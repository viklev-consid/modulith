namespace Modulith.Modules.Users.Domain;

public sealed record PasswordHash(string Value)
{
    public override string ToString() => "[REDACTED]";
}
