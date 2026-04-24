namespace Modulith.Modules.Users.Contracts.Events;

/// <summary>
/// Fired when a user is auto-provisioned from an external login confirm.
/// Distinct from UserRegisteredV1, which is narrowed to password registration only.
/// </summary>
public sealed record UserProvisionedFromExternalV1(
    Guid UserId,
    string Provider,
    string Subject,
    string Email,
    string DisplayName,
    DateTimeOffset ProvisionedAt,
    Guid EventId);
