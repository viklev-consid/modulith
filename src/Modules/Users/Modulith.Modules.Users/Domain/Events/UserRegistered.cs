using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

internal sealed record UserRegistered(UserId UserId, string Email, string DisplayName) : DomainEvent;
