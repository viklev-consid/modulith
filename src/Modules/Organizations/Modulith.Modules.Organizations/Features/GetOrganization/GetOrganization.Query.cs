using Modulith.Modules.Organizations.Domain;
using Modulith.Shared.Infrastructure.Authorization;

namespace Modulith.Modules.Organizations.Features.GetOrganization;

public sealed record GetOrganizationQuery(OrganizationId OrganizationId, ScopedAuthorizationAccessMode AccessMode);
