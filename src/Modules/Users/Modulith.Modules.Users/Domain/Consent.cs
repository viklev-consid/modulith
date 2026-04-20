using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain;

public sealed class Consent : Entity<ConsentId>
{
    private Consent() : base(new ConsentId(Guid.Empty)) { }

    private Consent(ConsentId id, Guid userId, string consentKey, bool granted, DateTimeOffset recordedAt)
        : base(id)
    {
        UserId = userId;
        ConsentKey = consentKey;
        Granted = granted;
        RecordedAt = recordedAt;
    }

    public Guid UserId { get; private set; }
    public string ConsentKey { get; private set; } = string.Empty;
    public bool Granted { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static Consent Grant(Guid userId, string consentKey, DateTimeOffset recordedAt)
        => new(new ConsentId(Guid.NewGuid()), userId, consentKey, granted: true, recordedAt);

    public static Consent Revoke(Guid userId, string consentKey, DateTimeOffset recordedAt)
        => new(new ConsentId(Guid.NewGuid()), userId, consentKey, granted: false, recordedAt);
}
