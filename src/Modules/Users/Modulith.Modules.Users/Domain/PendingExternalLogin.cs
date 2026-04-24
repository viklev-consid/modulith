using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class PendingExternalLogin : Entity<PendingExternalLoginId>
{
    private const int MaxUserAgentLength = 512;

    private PendingExternalLogin(
        PendingExternalLoginId id,
        ExternalLoginProvider provider,
        string subject,
        string email,
        string displayName,
        bool isExistingUser,
        byte[] tokenHash,
        string? createdFromIp,
        string? userAgent,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt) : base(id)
    {
        Provider = provider;
        Subject = subject;
        Email = email;
        DisplayName = displayName;
        IsExistingUser = isExistingUser;
        TokenHash = tokenHash;
        CreatedFromIp = createdFromIp;
        UserAgent = userAgent;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    // Required by EF Core for materialization.
    private PendingExternalLogin() : base(new PendingExternalLoginId(Guid.Empty)) { }

    public ExternalLoginProvider Provider { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Captured at creation time so email template selection is deterministic.</summary>
    public bool IsExistingUser { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public string? CreatedFromIp { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>
    /// Creates a pending record and returns the entity plus the raw confirmation token
    /// (to send via email — never persisted).
    /// </summary>
    public static (PendingExternalLogin pending, string rawValue) Create(
        ExternalLoginProvider provider,
        string subject,
        string email,
        string displayName,
        bool isExistingUser,
        string? createdFromIp,
        string? userAgent,
        TimeSpan lifetime,
        IClock clock)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        var rawValue = Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));

        var ua = userAgent?.Length > MaxUserAgentLength ? userAgent[..MaxUserAgentLength] : userAgent;

        var now = clock.UtcNow;
        var pending = new PendingExternalLogin(
            PendingExternalLoginId.New(),
            provider,
            subject,
            email,
            displayName,
            isExistingUser,
            hash,
            createdFromIp,
            ua,
            now,
            now.Add(lifetime));

        return (pending, rawValue);
    }

    public ErrorOr<Success> Consume(IClock clock)
    {
        if (!IsValid(clock))
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        ConsumedAt = clock.UtcNow;
        return Result.Success;
    }

    public bool IsValid(IClock clock) =>
        ConsumedAt is null && ExpiresAt > clock.UtcNow;

    public static byte[] HashRawValue(string rawValue) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
}
