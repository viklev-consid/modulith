---
name: authorization-model
description: System-level authorization model for Modulith. Covers single-role RBAC, permission declarations in Contracts, permission registration, per-request claim expansion, and role-change behavior.
---

# Authorization Model

Use this skill when you are changing the authorization model itself rather than just applying an access check to one endpoint.

Typical triggers:

- adding permissions to a module
- adding a new role
- changing how roles map to permissions
- modifying the RBAC infrastructure or permission registration flow
- deciding whether a change fits the existing RBAC model or needs an architectural decision

Do not use this skill when:

- the task is only adding `.RequireAuthorization(...)` to one endpoint
- the task is only login, logout, password reset, or refresh-token behavior
- the task is only ownership-aware resource checks at the endpoint boundary

## Read first

Before changing the authorization model, read:

1. `docs/adr/0030-rbac.md`
2. `docs/how-to/auth/use-rbac.md`
3. `src/Modules/Users/Modulith.Modules.Users/Security/Authorization/RbacServiceCollectionExtensions.cs`
4. `src/Shared/Modulith.Shared.Infrastructure/Authorization/PermissionSourceExtensions.cs`
5. one existing module's `*Permissions` class and module registration

## Core model

The live repo model is deliberately simple.

- each user has exactly one role
- endpoints authorize on permissions, not roles
- permissions are declared by modules in their `.Contracts` projects
- JWTs carry the role claim, not the full permission set
- permissions are resolved per request and exposed through permission claims and `ICurrentUser`

If your change fights those assumptions, stop and ask first.

## One role per user

This is a deliberate constraint, not an omission.

Implications:

- no user-role join table
- no partial role grants and revokes
- role changes are modeled as `ChangeRole`, not `AssignRole` or `RevokeRole`
- the token surface stays small and simple

Do not casually introduce multi-role behavior. That is a model change, not a local refactor.

## Permissions are module-owned contracts

Permission constants belong in the owning module's `.Contracts/Authorization/` folder.

Example shape:

```csharp
public static class OrdersPermissions
{
    public const string OrdersRead = "orders.orders.read";
    public const string OrdersWrite = "orders.orders.write";

    public static IReadOnlyCollection<string> All { get; } = [OrdersRead, OrdersWrite];
}
```

Why this matters:

- the authorization surface is part of the module's public contract
- endpoints and cross-module callers can reference the same constants
- architectural tests can enforce naming and ownership

## Permission naming rules

Use the existing format:

- `{module}.{resource}.{action}`
- lowercase
- ASCII
- module segment matches the owning module

Do not invent ad-hoc formats or role-prefixed permission names.

## Registration rules

Permissions are not just declared. They must be registered with the permission catalog.

Per module, call:

```csharp
services.AddPermissions(OrdersPermissions.All);
```

This belongs in the module's `Add*Module` registration.

At composition time, the API host calls:

```csharp
builder.Services.AddRbac();
```

Do not duplicate RBAC infrastructure setup inside individual modules.

## Claims model rules

The JWT carries the `role` claim only.

Per request:

- `IClaimsTransformation` resolves permissions from the role
- permission claims are added to the request principal
- `ICurrentUser` exposes both the role and the resolved permissions

Do not switch to stuffing all permissions into JWTs without an explicit architecture decision.

## Role to permission composition rules

Roles are static compositions of permissions defined in code.

Current defaults:

- `admin` -> all declared permissions
- `user` -> no extra permissions beyond authenticated-only access

Adding a role is a code change. That is intentional.

Do not build runtime role-editing concepts into the default model unless the architecture is being deliberately changed.

## Role change semantics

When a user's role changes:

- refresh tokens are revoked
- existing access tokens remain valid until natural expiry
- the user must re-authenticate to obtain a token with the new role claim

This bounded stale-role window is part of the design. If your requirement is immediate role revocation on every request, that is a different model.

## `ICurrentUser` rules

`ICurrentUser` exposes:

- caller identity
- current role
- resolved permission set
- convenience permission checks

Use it for conditional behavior and self-scoped access decisions.

Do not use it to replace endpoint-level policy declarations where a permission policy is the right abstraction.

## Frontend contract rules

The frontend-facing permission set is delivered through `/v1/users/me`.

Implications:

- the permission set in `/me` should stay consistent with the token's role
- permission changes are naturally synchronized through re-login after role change
- there is no need for a second default endpoint just to fetch permissions

If you want to change that contract, treat it as a deliberate design change.

## Common mistakes

Avoid these:

- putting permission constants in internal projects instead of `.Contracts`
- forgetting `services.AddPermissions(<ModulePermissions>.All)` in a module
- authorizing on role names directly instead of permission constants
- adding permission lists to JWTs because it looks simpler locally
- introducing multi-role semantics accidentally through ad-hoc checks
- treating runtime role editing as a local feature instead of a model change

## Ask-first cases

Stop and ask before proceeding if:

- the change introduces multi-role users
- the change moves permissions into JWTs or the database by default
- the change adds complex relationship or graph-based authorization as a default
- the change requires immediate access-token revocation semantics

## Definition of done

An authorization-model change is complete when:

- permissions are declared in the owning module's `.Contracts` project
- modules register their permission sets with `AddPermissions(...)`
- the change fits the single-role RBAC model unless explicitly escalated
- endpoint policies still consume permission constants, not roles
- role-change behavior and stale-token implications are understood and tested where needed

## Reference material

Use these as the source of truth:

- `docs/adr/0030-rbac.md`
- `docs/how-to/auth/use-rbac.md`
- `src/Modules/Users/Modulith.Modules.Users/Security/Authorization/RbacServiceCollectionExtensions.cs`
- `src/Shared/Modulith.Shared.Infrastructure/Authorization/PermissionSourceExtensions.cs`
- `src/Shared/Modulith.Shared.Kernel/Interfaces/ICurrentUser.cs`