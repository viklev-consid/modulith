using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

internal sealed record UserOnboardingCompleted(UserId UserId) : DomainEvent;
