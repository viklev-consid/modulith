using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

internal sealed record ExternalLoginLinked(
    UserId UserId,
    ExternalLoginProvider Provider,
    string Subject,
    DateTimeOffset LinkedAt) : DomainEvent;
