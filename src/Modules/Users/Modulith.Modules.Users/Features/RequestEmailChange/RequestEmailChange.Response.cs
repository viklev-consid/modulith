namespace Modulith.Modules.Users.Features.RequestEmailChange;

/// <summary>
/// Always returned — even when the email is taken — to prevent enumeration.
/// </summary>
public sealed record RequestEmailChangeResponse(string Message = "If that email address is available, a confirmation link has been sent to it.");
