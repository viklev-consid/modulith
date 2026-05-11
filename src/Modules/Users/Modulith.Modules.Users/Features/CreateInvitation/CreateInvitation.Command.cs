namespace Modulith.Modules.Users.Features.CreateInvitation;

public sealed record CreateInvitationCommand(string Email, Guid InvitedByUserId, string? IpAddress, string? UserAgent);
