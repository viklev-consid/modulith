namespace Modulith.Modules.Users.Features.Register;

public sealed record RegisterResponse(
    Guid UserId,
    string Message = "Registration successful. Check your email to confirm your account before signing in.",
    bool RequiresEmailConfirmation = true);
