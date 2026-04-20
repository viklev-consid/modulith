using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

internal sealed record UserEmailChanged(UserId UserId, string OldEmail, string NewEmail) : DomainEvent;
