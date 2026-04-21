namespace Modulith.Modules.Users.Features.ForgotPassword;

/// <summary>
/// Always returned — even when the email is not found — to prevent enumeration.
/// </summary>
public sealed record ForgotPasswordResponse(string Message = "If an account with that email exists, a reset link has been sent.");
