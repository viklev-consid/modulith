namespace Modulith.Modules.Users.Contracts.Events;

/// <summary>
/// Published when an authenticated user requests an email address change.
/// Carries the raw token so Notifications can send a confirmation link to the new address.
/// Treat the raw token as sensitive — do not log or persist beyond Notifications.
/// </summary>
public sealed record EmailChangeRequestedV1(Guid UserId, string NewEmail, string RawToken);
