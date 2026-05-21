namespace Modulith.Modules.Organizations.Features.CreateOrganization;

public sealed record CreateOrganizationRequest(string Name, string? Slug = null);
