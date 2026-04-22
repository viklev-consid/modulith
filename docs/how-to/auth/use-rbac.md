# How to use RBAC in Modulith

This guide explains how the RBAC system works and how to gate endpoints on permissions.

---

## How it works

Every user has one role stored in the `users.users` table (`role` varchar(32), default `'user'`). On each request, a `IClaimsTransformation` (`PermissionClaimsTransformation`) reads the `ClaimTypes.Role` claim from the JWT and adds the permissions for that role as individual `"permission"` claims.

Each permission is a constant string following the format **`{module}.{resource}.{action}`** (all lowercase, three dot-separated segments). Permission constants live in each module's `.Contracts/Authorization/` folder.

The `PermissionCatalog` singleton discovers all `*Permissions` types in `*.Contracts` assemblies at startup and builds the role→permissions map:

| Role    | Permissions                          |
|---------|--------------------------------------|
| `admin` | all declared permission constants    |
| `user`  | *(empty — no extra permissions)*     |

---

## Declaring permissions for a new module

Add a static class in your module's `.Contracts` project under `Authorization/`:

```csharp
// src/Modules/Orders/Modulith.Modules.Orders.Contracts/Authorization/OrdersPermissions.cs
namespace Modulith.Modules.Orders.Contracts.Authorization;

public static class OrdersPermissions
{
    public const string OrdersRead  = "orders.orders.read";
    public const string OrdersWrite = "orders.orders.write";

    public static IReadOnlyCollection<string> All { get; } = [OrdersRead, OrdersWrite];
}
```

The `PermissionCatalog` picks these up automatically at startup — no registration needed.

Naming rules (enforced by architectural tests):

- Three dot-separated lowercase segments: `{module}.{resource}.{action}`
- Module segment matches the module name (lower-case, e.g. `orders`, `catalog`, `users`)
- Action is one of: `read`, `write`, `delete`, or similar verb

---

## Protecting an endpoint

```csharp
// In your Endpoint.cs
app.MapPut("/v1/orders/{orderId}/cancel", ...)
   .RequireAuthorization(OrdersPermissions.OrdersWrite);
```

Prefer using the constant over a raw string to keep the authorization check refactoring-safe and verifiable by the architectural tests.

---

## Accessing role and permissions in a handler

Inject `ICurrentUser`:

```csharp
public sealed class MyHandler(ICurrentUser currentUser)
{
    public Task<Result> Handle(MyCommand cmd, CancellationToken ct)
    {
        var role = currentUser.Role;                   // "admin" or "user"
        var canWrite = currentUser.HasPermission(OrdersPermissions.OrdersWrite);
        // ...
    }
}
```

---

## Built-in roles

| Role    | Description                               |
|---------|-------------------------------------------|
| `user`  | Default role assigned at registration.    |
| `admin` | Full access — all permissions.            |

Roles are validated by the `Role` value object (regex `^[a-z][a-z0-9_-]{1,31}$`). New roles require an explicit entry in the `PermissionCatalog`'s role→permissions map.

---

## GET /v1/users/me response

The `/me` endpoint returns the caller's current role, sorted permissions, and a version hash:

```json
{
  "userId": "...",
  "email": "...",
  "displayName": "...",
  "createdAt": "...",
  "role": "admin",
  "permissions": ["audit.trail.read", "catalog.products.read", "..."],
  "permissionsVersion": "abc123..."
}
```

The `permissionsVersion` is a SHA-256 base64url hash of the sorted permissions list. Clients can cache permissions locally and use this hash to detect when re-fetching is needed.

The endpoint sets `Cache-Control: private, no-store`.

---

## Changing a user's role

`PUT /v1/users/{userId}/role` — requires `users.roles.write` (admin only).

```json
{ "role": "admin" }
```

Role changes:
- Revoke all refresh tokens for the target user (forcing re-login to get a new JWT with the updated role).
- Are recorded in the audit trail as `user.role_changed`.
- Cannot target the caller's own account (self-role-change is rejected with 422).

**Stale-permission window.** Role changes take full effect only when the user's current access token expires and they re-authenticate. Until then, a demoted admin retains their old permissions for up to `AccessTokenLifetimeMinutes` (default: 15 minutes). `GET /v1/users/me` reflects the same role that the token carries — it does not hit the database for the role — so `role` and `permissions` in the `/me` response are always internally consistent with what the token authorizes.

This is the standard stateless-JWT tradeoff. If your threat model requires immediate revocation on demotion, you need server-side access-token versioning (a security stamp checked on every request). That is an architectural shift documented in ADR-0030.

---

## See also

- [Bootstrap the first admin account](bootstrap-admin.md)
- ADR-0030: RBAC design rationale
