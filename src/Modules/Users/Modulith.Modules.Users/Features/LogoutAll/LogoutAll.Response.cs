namespace Modulith.Modules.Users.Features.LogoutAll;

/// <summary>No request body needed — the authenticated user's ID comes from the JWT.</summary>
public sealed record LogoutAllResponse(string Message = "Logged out from all devices successfully.");
