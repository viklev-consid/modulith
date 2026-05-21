using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Features.UpdateOrganization;

public sealed record UpdateOrganizationCommand(OrganizationId OrganizationId, string Name, string Slug);
