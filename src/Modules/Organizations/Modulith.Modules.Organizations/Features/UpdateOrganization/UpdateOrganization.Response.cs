namespace Modulith.Modules.Organizations.Features.UpdateOrganization;

public sealed record UpdateOrganizationResponse(Guid OrganizationId, string Name, string Slug);
