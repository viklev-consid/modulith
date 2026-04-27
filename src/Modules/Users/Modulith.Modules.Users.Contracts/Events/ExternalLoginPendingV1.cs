namespace Modulith.Modules.Users.Contracts.Events;

/// <summary>
/// Published when an external login confirmation is pending.
/// RawToken is for Notifications only — the sole subscriber — and must not be logged or re-published.
/// </summary>
public sealed record ExternalLoginPendingV1(
    string Provider,
    string Email,
    string DisplayName,
    bool IsExistingUser,
    string RawToken,
    Guid EventId);
