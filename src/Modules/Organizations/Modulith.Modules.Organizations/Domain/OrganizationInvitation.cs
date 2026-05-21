using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Modulith.Modules.Organizations.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.Domain;

public sealed class OrganizationInvitation : Entity<OrganizationInvitationId>
{
    private OrganizationInvitation(
        OrganizationInvitationId id,
        OrganizationId organizationId,
        string email,
        OrganizationRole role,
        byte[] tokenHash,
        DateTimeOffset invitedAt,
        DateTimeOffset expiresAt,
        Guid invitedByUserId) : base(id)
    {
        OrganizationId = organizationId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        InvitedAt = invitedAt;
        ExpiresAt = expiresAt;
        InvitedByUserId = invitedByUserId;
        IsPending = true;
    }

    private OrganizationInvitation() : base(default!) { }

    public OrganizationId OrganizationId { get; private set; } = null!;
    public string Email { get; private set; } = string.Empty;
    public OrganizationRole Role { get; private set; } = OrganizationRole.Member;
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset InvitedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public Guid? AcceptedUserId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public bool IsPending { get; private set; }

    public static ErrorOr<(OrganizationInvitation Invitation, string RawToken)> Create(
        OrganizationId organizationId,
        string email,
        OrganizationRole role,
        TimeSpan lifetime,
        Guid invitedByUserId,
        IClock clock)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            return OrganizationsErrors.InvitationLifetimeInvalid;
        }

        var rawToken = GenerateRawToken();
        var now = clock.UtcNow;
        return (
            new OrganizationInvitation(
                OrganizationInvitationId.New(),
                organizationId,
                email.Trim().ToLowerInvariant(),
                role,
                HashRawValue(rawToken),
                now,
                now.Add(lifetime),
                invitedByUserId),
            rawToken);
    }

    public ErrorOr<Success> Accept(Guid userId, string email, IClock clock)
    {
        // Both values are normalized to lowercase before comparison.
        if (!string.Equals(Email, email.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return OrganizationsErrors.InvitationInvalid;
        }

        if (!CanBeAccepted(clock))
        {
            return OrganizationsErrors.InvitationInvalid;
        }

        AcceptedAt = clock.UtcNow;
        AcceptedUserId = userId;
        IsPending = false;
        return Result.Success;
    }

    public ErrorOr<Success> Revoke(Guid revokedByUserId, IClock clock)
    {
        if (AcceptedAt is not null)
        {
            return OrganizationsErrors.InvitationAlreadyAccepted;
        }

        if (RevokedAt is not null)
        {
            return OrganizationsErrors.InvitationAlreadyRevoked;
        }

        RevokedAt = clock.UtcNow;
        RevokedByUserId = revokedByUserId;
        IsPending = false;
        return Result.Success;
    }

    public bool CanBeAccepted(IClock clock) =>
        IsPending && ExpiresAt > clock.UtcNow;

    public static byte[] HashRawValue(string rawValue) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));

    private static string GenerateRawToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
