using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

internal sealed record UserProvisionedFromExternal(
    UserId UserId,
    ExternalLoginProvider Provider,
    string Subject,
    string Email,
    string DisplayName,
    DateTimeOffset ProvisionedAt) : DomainEvent;
