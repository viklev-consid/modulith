namespace Modulith.Modules.Organizations.Features.CreateOrganization;

public sealed record CreateOrganizationResponse(Guid OrganizationId, string Name, string Slug, string Role);
