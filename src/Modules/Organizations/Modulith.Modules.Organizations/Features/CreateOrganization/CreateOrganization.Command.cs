namespace Modulith.Modules.Organizations.Features.CreateOrganization;

public sealed record CreateOrganizationCommand(string Name, string? Slug, Guid CreatedByUserId);
