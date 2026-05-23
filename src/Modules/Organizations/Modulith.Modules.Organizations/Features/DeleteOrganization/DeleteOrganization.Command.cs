using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Features.DeleteOrganization;

public sealed record DeleteOrganizationCommand(OrganizationId OrganizationId, Guid DeletedByUserId);
