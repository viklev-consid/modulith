---
name: access-control
description: Endpoint-level guidance for applying authentication and authorization in Modulith. Covers RequireAuthorization, ICurrentUser, permission checks, and ownership-aware resource policies.
---

# Access Control

Use this skill when you are protecting an endpoint or query based on who the caller is and what they may access.

Typical triggers:

- adding `.RequireAuthorization(...)` to endpoints
- deciding between authenticated-only, permission-gated, and ownership-aware access
- using `ICurrentUser` in endpoint code
- adding a resource policy such as permission-or-owner access

Do not use this skill when:

- the task is changing the system-wide role and permission model
- the task is implementing login, logout, refresh-token, or password-reset flows
- the task is only generic slice creation with no access decision

## Read first

Before changing access checks, read:

1. `docs/how-to/add-a-slice.md`
2. `docs/how-to/auth/use-rbac.md`
3. `src/Shared/Modulith.Shared.Infrastructure/Authorization/IResourcePolicy.cs`
4. `src/Shared/Modulith.Shared.Infrastructure/Authorization/PermissionOrOwnerPolicy.cs`
5. one nearby endpoint that already protects a similar resource

## The three common access patterns

Use one of these patterns.

### 1. Authenticated-only

Use this when the endpoint is scoped to the caller's own account or session and no extra permission model is needed.

Examples:

- `/v1/users/me`
- `/v1/users/me/password`
- `/v1/users/logout`
- `/v1/users/me/personal-data`

Pattern:

```csharp
.RequireAuthorization()
```

### 2. Permission-gated

Use this when a capability is explicitly admin or operator controlled and there is no ownership fallback.

Pattern:

```csharp
.RequireAuthorization(CatalogPermissions.ProductsWrite)
```

Prefer module-owned permission constants over raw strings.

### 3. Ownership-aware resource access

Use this when callers may access their own resources, while elevated permissions allow broad access.

Examples:

- an admin can read any audit trail
- a normal user can read only their own audit trail

Pattern:

- `.RequireAuthorization()` at the endpoint
- resolve `ICurrentUser`
- apply `IResourcePolicy<TResource>` in the endpoint
- keep the handler pure

## Keep authorization at the HTTP boundary

The endpoint is the right place for HTTP-caller-specific authorization.

Why:

- handlers may be invoked by other modules or background jobs
- putting `ICurrentUser` into handlers quietly couples them to HTTP caller context
- resource-authorization decisions belong at the boundary where the caller identity exists

This is especially important for queries that may later become cross-module contracts.

## `ICurrentUser` usage rules

`ICurrentUser` is the shared abstraction for the current caller.

Use it in endpoints for:

- self-scoped routes
- ownership-aware resource checks
- command construction when the caller's ID is part of the command

Use it in handlers only when behavior is legitimately conditional on the caller context and the handler is not meant to be a generic reusable contract entry point.

Do not use it as a substitute for endpoint authorization policies.

## Permission-gated endpoint rules

When an endpoint needs a declared permission:

- reference the permission constant from the owning module's `.Contracts/Authorization/` project
- call `.RequireAuthorization(<PermissionConst>)`
- do not duplicate the permission string inline
- do not authorize on roles directly at the endpoint

Permissions are the runtime unit. Roles are how those permissions are composed.

## Ownership-aware resource policy rules

Use `IResourcePolicy<TResource>` for resource-instance decisions.

For the common pattern of elevated-permission-or-owner, derive from `PermissionOrOwnerPolicy<TResource>`.

Implementation shape:

1. define a lightweight resource scope type if needed
2. implement the policy in the module
3. register it in the module's service registration
4. apply it in the endpoint before dispatching to the handler

Example policy:

```csharp
internal sealed record AuditTrailResource(Guid ActorId);

internal sealed class AuditTrailPolicy : PermissionOrOwnerPolicy<AuditTrailResource>
{
    protected override string ElevatedPermission => AuditPermissions.TrailRead;
    protected override string? GetOwnerId(AuditTrailResource resource) => resource.ActorId.ToString();
}
```

Example endpoint use:

```csharp
if (!policy.IsAuthorized(currentUser, resource))
{
    return Results.Forbid();
}
```

## Registration rules

For resource policies, register the policy in the owning module:

```csharp
services.AddSingleton<IResourcePolicy<AuditTrailResource>, AuditTrailPolicy>();
```

Do not register resource policies in unrelated modules.

## When not to use a resource policy

Do not create a policy when:

- the endpoint is purely permission-gated
- the endpoint is purely authenticated self-access with no ownership branching
- the handler can determine correctness from data invariants rather than caller identity

Resource policies are for access decisions, not general business rules.

## Common mistakes

Avoid these:

- putting ownership checks inside handlers
- injecting `ICurrentUser` into shared query handlers that may be invoked outside HTTP
- authorizing on raw permission strings instead of constants
- authorizing on role names directly at endpoints
- using resource policies for endpoints that only need `.RequireAuthorization(<Permission>)`
- forgetting to register the policy in DI

## Ask-first cases

Stop and ask before proceeding if:

- the access model needs multi-tenant membership or delegated access beyond owner-or-permission
- the change implies a new system-wide authorization primitive
- the endpoint seems to need both cross-module data and caller-aware authorization in a way that blurs boundaries

## Definition of done

An access-control change is complete when:

- the endpoint uses the correct access pattern for the use case
- permission checks use module-owned constants
- ownership-aware checks live at the endpoint boundary
- handlers remain pure where they need to be reusable
- any resource policy is registered in the owning module
- integration tests cover allowed and forbidden cases

## Reference material

Use these as the source of truth:

- `docs/how-to/add-a-slice.md`
- `docs/how-to/auth/use-rbac.md`
- `src/Shared/Modulith.Shared.Infrastructure/Authorization/IResourcePolicy.cs`
- `src/Shared/Modulith.Shared.Infrastructure/Authorization/PermissionOrOwnerPolicy.cs`
- `src/Shared/Modulith.Shared.Kernel/Interfaces/ICurrentUser.cs`