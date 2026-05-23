# ADR-0035: Organizations and Scoped Authorization

## Status

Accepted

## Context

The template has a mature Users module and a deliberately simple global RBAC model: each user has one global role, endpoints authorize on module-owned permissions, and JWTs carry the role while per-request claims transformation expands it into permissions. That works well for single-user apps, consumer apps, and platform administration.

Many products also need a workspace/account primitive: users collaborate inside companies, clinics, schools, vendors, projects, or teams. Those products need membership roles, invitations, organization settings, organization-owned resources, and authorization decisions like "Alice may manage members in Organization A but not Organization B."

This is not the same as full infrastructure-level multi-tenancy. We want an application-level organization boundary that future modules can opt into without requiring separate databases, schemas, caches, blob stores, job queues, or deployment units per tenant.

## Decision

Introduce a first-class Organizations module and shared scoped-authorization abstractions.

### Organizations are workspace/account scope, not full multi-tenancy

An organization is an application-level ownership and collaboration boundary. Modules may store `OrganizationId` on resources and evaluate permissions against that organization.

This does not imply:

- separate database or schema per organization
- automatic tenant filters on every query
- tenant-partitioned caches, blob containers, search indexes, jobs, or observability
- invisibility of one organization to platform administrators

Modules remain independently owned and schema-scoped. Cross-module references to organizations use IDs and public contracts, never direct access to Organizations internals.

### Organizations live in their own module

Users continues to own identity lifecycle: registration, authentication, refresh tokens, global roles, legal acceptance, and GDPR user erasure orchestration.

Organizations owns:

- organizations
- organization slugs
- memberships
- organization membership roles
- organization invitations
- organization-scoped authorization evaluation
- organization integration events

This keeps identity separate from collaboration/account membership.

### Route scope uses `organizationRef`

Organization-scoped routes use this default shape:

```text
/v1/organizations/{organizationRef}/...
```

`organizationRef` accepts either the organization's stable ID or its slug. Endpoints resolve it at the HTTP boundary, then commands, queries, persisted references, and public events use `OrganizationId`.

Slugs are globally unique across active and soft-deleted organizations in v1. Slug changes are allowed, but old-slug history and redirects are intentionally deferred.

### Organization roles are scoped membership roles

Each active membership has exactly one organization role. Initial roles:

- `owner`
- `admin`
- `member`
- `viewer`

The role-to-permission map is code-defined. Runtime role administration is not part of the default template.

Active organizations must have at least one owner. The last owner cannot leave, be removed, or be demoted. Deleting the whole organization is a separate explicit operation and is allowed for an owner.

### Scoped permissions are evaluated per request

Scoped permissions are not placed in JWTs and are not appended to the flat permission list returned by `/v1/users/me`.

Instead, shared abstractions allow modules to evaluate:

```text
current user + organization scope + permission + options
```

The Organizations module provides the first concrete evaluator for `OrganizationScope`. Future modules can store `OrganizationId`, resolve `organizationRef` at the endpoint boundary, and authorize through the shared scoped-authorization API without referencing Organizations internals.

### Platform override is explicit

Global admins may bypass organization membership only when an endpoint or policy explicitly opts into platform override. Platform override does not create hidden membership and must not make global admins appear in member lists, owner counts, notifications, or membership history.

The platform override permission is:

```text
organizations.platform.override
```

Scoped authorization results expose the access mode (`ScopedPermission` vs. `PlatformOverride`) so audit trails can record how access was granted. If a platform admin is also an active organization member, membership wins and the access mode is `ScopedPermission`; `PlatformOverride` is reserved for access that truly bypassed membership.

### Organization invitations may onboard users

Organization invitations are scoped to an organization, email, and requested organization role.

If the invitee already has an account, accepting the invitation creates the membership. If the email does not belong to an existing account, the invite flow routes through Users-owned registration. Users creates a normal global `user` account, then the organization invitation is consumed to create membership. Registration must not silently succeed if consuming the organization invitation fails; the API surfaces the failure and compensates the just-created user record where possible.

Organizations does not create users through internal Users APIs. Any synchronous cross-module interaction goes through public contracts.

Raw invitation tokens may appear in the invitation-created integration event so Notifications can build the email link. They are marked sensitive, stored only as hashes in Organizations, omitted from Audit payloads, and should not be logged by subscribers.

### Organization deletion is soft-delete in v1

Deleting an organization marks it deleted and disables normal membership, invitation, and resource access. Historical records remain for audit and business continuity. Hard purge is a separate retention decision.

### GDPR keeps business records and redacts personal identity

User profile data remains personal data even when the user acted inside an organization. Organization business records may still need to remain after user erasure.

When deleting a user:

- if the user owns active organizations, deletion is blocked until ownership is transferred or the organization is deleted
- if the user is only a member, active memberships are removed
- membership history and audit records are retained where appropriate, but personal identity fields are anonymized
- retained membership rows clear the erased user's `UserId`; they are no longer queryable as that user
- organization-owned business records remain unless a separate organization deletion/retention policy removes them

### Audit and notifications carry organization context

Organization actions publish integration events. Notifications may include optional organization context so users can be notified about invites, role changes, removals, ownership transfers, and organization deletion.

Audit records include optional organization scope and access mode for organization-related actions.

## Consequences

### Positive

- The template supports single-user apps and organization-based apps without forcing every module into organization scope.
- Future modules have a standard way to attach resources to organizations.
- Scoped authorization is explicit, testable, and reusable.
- Global platform override is powerful but visible and auditable.
- Slug URLs are ergonomic while IDs remain the durable reference.
- Organization membership does not bloat JWTs or the global `/me` permission list.

### Negative

- Organization authorization introduces a database lookup or cache lookup per scoped authorization decision.
- Modules that opt into organizations must write cross-org denial tests.
- Invite acceptance for unknown users requires a coordinated Users/Organizations flow.
- Soft deletion and GDPR anonymization add lifecycle complexity.

### Neutral

- This is not a billing/account ownership system. Billing, subscription ownership, quotas, and invoices are intentionally out of scope.
- This is not full multi-tenancy. Products needing tenant-isolated infrastructure can build on the organization primitive later.
- Teams/subgroups inside organizations are intentionally deferred.

## Related

- ADR-0001: Modular Monolith Architecture
- ADR-0005: Module Communication Patterns
- ADR-0007: No ASP.NET Identity
- ADR-0012: GDPR Primitives Baked Into the Template
- ADR-0015: Architectural Tests
- ADR-0023: Per-Module DbContext
- ADR-0030: Role-Based Access Control with Permission-Level Policies
