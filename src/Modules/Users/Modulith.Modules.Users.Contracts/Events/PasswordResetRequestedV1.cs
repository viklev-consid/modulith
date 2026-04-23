namespace Modulith.Modules.Users.Contracts.Events;

/// <summary>
/// Published when a password reset email is requested.
/// Carries the raw token so Notifications can embed it in the email link.
/// Treat this event's raw token as sensitive — do not log or persist beyond Notifications.
/// </summary>
public sealed record PasswordResetRequestedV1(Guid UserId, string Email, string RawToken, Guid EventId);
