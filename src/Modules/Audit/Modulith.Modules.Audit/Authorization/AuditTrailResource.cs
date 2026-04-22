namespace Modulith.Modules.Audit.Authorization;

/// <summary>
/// Represents the audit trail scope for a given actor — used as the resource
/// in <see cref="AuditTrailPolicy"/> authorization checks.
/// </summary>
public sealed record AuditTrailResource(Guid ActorId);
