namespace Modulith.Modules.Audit.Contracts.Dtos;

public sealed record AuditEntryDto(
    Guid Id,
    string EventType,
    Guid? ActorId,
    string? ResourceType,
    Guid? ResourceId,
    DateTimeOffset OccurredAt);
