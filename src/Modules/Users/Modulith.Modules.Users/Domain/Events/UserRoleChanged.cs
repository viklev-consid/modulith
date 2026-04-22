using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

internal sealed record UserRoleChanged(
    UserId UserId,
    string OldRole,
    string NewRole,
    UserId ChangedBy) : DomainEvent;
