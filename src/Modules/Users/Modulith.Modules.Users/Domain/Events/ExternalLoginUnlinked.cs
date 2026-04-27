using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

internal sealed record ExternalLoginUnlinked(
    UserId UserId,
    ExternalLoginProvider Provider) : DomainEvent;
