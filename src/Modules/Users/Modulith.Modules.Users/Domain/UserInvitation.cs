using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class UserInvitation : Entity<UserInvitationId>
{
    private UserInvitation(
        UserInvitationId id,
        string email,
        byte[] tokenHash,
        DateTimeOffset invitedAt,
        DateTimeOffset expiresAt,
        UserId? invitedByUserId,
        string? createdFromIp,
        string? userAgent) : base(id)
    {
        Email = email;
        TokenHash = tokenHash;
        InvitedAt = invitedAt;
        ExpiresAt = expiresAt;
        InvitedByUserId = invitedByUserId;
        CreatedFromIp = createdFromIp;
        UserAgent = userAgent;
    }

    private UserInvitation() : base(new UserInvitationId(Guid.Empty)) { }

    public string Email { get; private set; } = string.Empty;
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset InvitedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public UserId? InvitedByUserId { get; private set; }
    public string? CreatedFromIp { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public UserId? AcceptedUserId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static ErrorOr<(UserInvitation invitation, string rawToken)> Create(
        Email email,
        TimeSpan lifetime,
        IClock clock,
        UserId? invitedByUserId = null,
        string? createdFromIp = null,
        string? userAgent = null)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            return UsersErrors.InvitationLifetimeInvalid;
        }

        var rawToken = GenerateRawToken();
        var now = clock.UtcNow;

        return (
            new UserInvitation(
                UserInvitationId.New(),
                email.Value,
                HashRawValue(rawToken),
                now,
                now.Add(lifetime),
                invitedByUserId,
                createdFromIp,
                Truncate(userAgent, 512)),
            rawToken);
    }

    public ErrorOr<Success> Accept(UserId userId, Email email, IClock clock)
    {
        if (!string.Equals(Email, email.Value, StringComparison.Ordinal))
        {
            return UsersErrors.InvitationInvalid;
        }

        if (!IsPending(clock))
        {
            return UsersErrors.InvitationInvalid;
        }

        AcceptedAt = clock.UtcNow;
        AcceptedUserId = userId;
        return Result.Success;
    }

    public ErrorOr<Success> Revoke(IClock clock)
    {
        if (AcceptedAt is not null)
        {
            return UsersErrors.InvitationAlreadyAccepted;
        }

        if (RevokedAt is not null)
        {
            return UsersErrors.InvitationAlreadyRevoked;
        }

        RevokedAt = clock.UtcNow;
        return Result.Success;
    }

    public bool IsPending(IClock clock) =>
        AcceptedAt is null && RevokedAt is null && ExpiresAt > clock.UtcNow;

    public static byte[] HashRawValue(string rawValue) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));

    private static string GenerateRawToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
