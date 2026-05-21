# Use Organization Authorization

Organization authorization checks a permission against a specific organization.

This is different from global RBAC:

```text
Global: user has users.users.read
Scoped: user has organizations.members.manage in Organization A
```

## Concepts

Generic scoped authorization abstractions live in Shared. The Organizations module provides the implementation for:

```csharp
OrganizationScope
```

The authorization input is:

```text
current user + organization ID + permission + options
```

The result includes whether access succeeded and how it was granted:

```text
ScopedPermission
PlatformOverride
None
```

Audit code should preserve that access mode for organization-related actions.

## Platform override

Global admins may bypass organization membership only when the endpoint explicitly opts in.

Use the explicit option:

```csharp
ScopedAuthorizationOptions.AllowPlatformOverride
```

Do not model platform admins as hidden organization members. They must not appear in member lists, owner counts, membership history, or organization notifications unless they are real members.

## Permission ownership

Permissions belong to the module that owns the capability.

For Organizations-owned capabilities, use:

```text
organizations.organizations.read
organizations.organizations.write
organizations.members.read
organizations.members.manage
organizations.invitations.manage
organizations.audit.read
organizations.platform.override
```

For another module's organization-owned resources, declare permission constants in that module's `.Contracts/Authorization` folder, then evaluate them against `OrganizationScope`.

## Frontend contract

Do not add organization-scoped permissions to the flat `/v1/users/me.permissions` list.

Use the Organizations-owned current-user endpoint to hydrate organization memberships, roles, scoped permissions, and scoped permission versions.
