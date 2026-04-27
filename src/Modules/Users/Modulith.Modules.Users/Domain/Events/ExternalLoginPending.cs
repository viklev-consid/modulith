using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

/// <summary>
/// Carries the raw confirmation token for Notifications only — the sole subscriber.
/// Never log or persist the RawToken beyond the outbox delivery.
/// </summary>
internal sealed record ExternalLoginPending(
    ExternalLoginProvider Provider,
    string Email,
    string DisplayName,
    bool IsExistingUser,
    string RawToken) : DomainEvent;
