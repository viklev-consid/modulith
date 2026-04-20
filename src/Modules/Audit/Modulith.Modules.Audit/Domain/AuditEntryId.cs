using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Audit.Domain;

public sealed record AuditEntryId(Guid Value) : TypedId<Guid>(Value);
