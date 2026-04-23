using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Audit.Domain;

public sealed class AuditEntry : Entity<AuditEntryId>
{
    private AuditEntry() : base(new AuditEntryId(Guid.Empty)) { }

    private AuditEntry(
        AuditEntryId id,
        string eventType,
        Guid? actorId,
        string? resourceType,
        Guid? resourceId,
        string payload,
        DateTimeOffset occurredAt) : base(id)
    {
        EventType = eventType;
        ActorId = actorId;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public string EventType { get; private set; } = string.Empty;
    public Guid? ActorId { get; private set; }
    public string? ResourceType { get; private set; }
    public Guid? ResourceId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditEntry Create(
        string eventType,
        Guid? actorId,
        string? resourceType,
        Guid? resourceId,
        string payload,
        DateTimeOffset occurredAt)
        => new(
            new AuditEntryId(Guid.NewGuid()),
            eventType,
            actorId,
            resourceType,
            resourceId,
            payload,
            occurredAt);

    public void Anonymize(Guid userId, string redactedPayload)
    {
        if (ActorId == userId) { ActorId = null; }
        if (ResourceId == userId) { ResourceId = null; }
        Payload = redactedPayload;
    }
}
