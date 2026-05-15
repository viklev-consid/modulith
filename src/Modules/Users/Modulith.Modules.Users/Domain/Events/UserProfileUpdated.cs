using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

internal sealed record UserProfileUpdated(UserId UserId, string OldDisplayName, string NewDisplayName) : DomainEvent;
