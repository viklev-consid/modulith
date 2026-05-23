using System.ComponentModel.DataAnnotations;

namespace Modulith.Modules.Organizations;

public sealed class OrganizationsOptions
{
    [Range(1, 365)]
    public int InvitationLifetimeDays { get; init; } = 14;

    public TimeSpan InvitationLifetime => TimeSpan.FromDays(InvitationLifetimeDays);
}
