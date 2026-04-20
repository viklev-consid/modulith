using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Notifications.Domain;

public sealed record NotificationLogId(Guid Value) : TypedId<Guid>(Value);
