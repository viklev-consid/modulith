using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain;

public sealed class Consent : Entity<ConsentId>
{
    private const int MaxUserAgentLength = 512;

    private Consent() : base(new ConsentId(Guid.Empty)) { }

    private Consent(
        ConsentId id,
        Guid userId,
        string consentKey,
        bool granted,
        DateTimeOffset recordedAt,
        string? grantedFromIp,
        string? grantedUserAgent)
        : base(id)
    {
        UserId = userId;
        ConsentKey = consentKey;
        Granted = granted;
        RecordedAt = recordedAt;
        GrantedFromIp = grantedFromIp;
        GrantedUserAgent = grantedUserAgent;
    }

    public Guid UserId { get; private set; }
    public string ConsentKey { get; private set; } = string.Empty;
    public bool Granted { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>IP address of the client at consent time. Null for consents granted before this field was added.</summary>
    public string? GrantedFromIp { get; private set; }

    /// <summary>User-Agent of the client at consent time. Null for consents granted before this field was added.</summary>
    public string? GrantedUserAgent { get; private set; }

    public static Consent Grant(
        Guid userId,
        string consentKey,
        DateTimeOffset recordedAt,
        string? grantedFromIp = null,
        string? grantedUserAgent = null)
    {
        var ua = grantedUserAgent?.Length > MaxUserAgentLength ? grantedUserAgent[..MaxUserAgentLength] : grantedUserAgent;
        return new(new ConsentId(Guid.NewGuid()), userId, consentKey, granted: true, recordedAt, grantedFromIp, ua);
    }

    public static Consent Revoke(Guid userId, string consentKey, DateTimeOffset recordedAt)
        => new(new ConsentId(Guid.NewGuid()), userId, consentKey, granted: false, recordedAt, null, null);
}
