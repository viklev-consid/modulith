# Add an Organization-Scoped Feature

Use this guide when a feature belongs to a specific organization/workspace.

Organization scope is opt-in. Single-user features can continue to use `/v1/users/me/...` and authenticated-only or global permission checks.

## Default route shape

Use:

```text
/v1/organizations/{organizationRef}/...
```

`organizationRef` may be either the organization ID or slug. Resolve it at the endpoint boundary and pass `OrganizationId` into commands and queries.

Do not store or publish slugs as durable references. Slugs are API/user-facing identifiers; IDs are the internal and cross-module identifier.

## Standard workflow

1. Add `OrganizationId` to the aggregate or resource that belongs to an organization.
2. Add route constants under the owning module's routes file using `/v1/organizations/{organizationRef}/...`.
3. In the endpoint, resolve `organizationRef` to `OrganizationId`.
4. Authorize through `IScopedAuthorizationService<OrganizationScope>`.
5. Dispatch a command/query containing `OrganizationId`.
6. Keep handlers pure. Do not inject `ICurrentUser` only to gate HTTP access.
7. Add integration tests for allowed access, forbidden cross-org access, and platform override if enabled.

## Endpoint pattern

```csharp
var organization = await organizationResolver.ResolveAsync(organizationRef, ct);
if (organization.IsError)
{
    return organization.ToProblemDetails();
}

var authorization = await scopedAuthorization.AuthorizeAsync(
    currentUser,
    new OrganizationScope(organization.Value.Id),
    ProjectsPermissions.ProjectsWrite,
    ScopedAuthorizationOptions.RequireScopedPermission,
    ct);

if (!authorization.Succeeded)
{
    return Results.Forbid();
}

var command = new CreateProjectCommand(organization.Value.Id, request.Name);
var result = await bus.InvokeAsync<ErrorOr<CreateProjectResponse>>(command, ct);
return result.ToProblemDetailsOr(Results.Created);
```

Use platform override only when the endpoint is truly an operator/support endpoint:

```csharp
ScopedAuthorizationOptions.WithPlatformOverride
```

## Cross-module rules

- Reference `Modulith.Modules.Organizations.Contracts`, not the Organizations internal project.
- Store organization IDs as values in your module's schema. Do not add cross-schema foreign keys.
- Do not query Organizations tables from another module.
- Use public contracts or integration events if another module needs organization data.

## Testing checklist

Cover:

- member with permission succeeds
- member without permission is forbidden
- non-member is forbidden
- same user with access to Org A cannot access Org B
- platform override succeeds only on endpoints that opt in
- global admin does not appear as an implicit organization member
