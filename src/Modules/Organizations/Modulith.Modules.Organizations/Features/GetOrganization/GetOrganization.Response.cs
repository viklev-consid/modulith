namespace Modulith.Modules.Organizations.Features.GetOrganization;

public sealed record GetOrganizationResponse(Guid OrganizationId, string Name, string Slug, string AccessMode);
