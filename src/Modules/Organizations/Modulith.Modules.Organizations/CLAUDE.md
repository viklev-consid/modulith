# CLAUDE.md - Organizations Module

This module owns the application-level workspace/account primitive: organizations, slugs, memberships, invitations, and organization-scoped authorization. It is not full infrastructure-level multi-tenancy; modules opt into organization ownership by storing an `OrganizationId` and authorizing against an organization scope.

For general module conventions, see [`../../CLAUDE.md`](../../CLAUDE.md). For the architectural decision, see [`../../../../docs/adr/0035-organizations-and-scoped-authorization.md`](../../../../docs/adr/0035-organizations-and-scoped-authorization.md).

---

## Domain vocabulary

- **Organization** - the root aggregate. Identified by `OrganizationId` (typed Guid).
- **OrganizationSlug** - globally unique URL-friendly reference. Routes accept either slug or organization ID as `organizationRef`.
- **OrganizationMembership** - a user's active or historical role in an organization.
- **OrganizationRole** - `owner`, `admin`, `member`, or `viewer`, with rank used to prevent privilege escalation.
- **OrganizationInvitation** - email-scoped, single-use invitation into one organization with one requested role. Raw token exists only in transit; the stored token is a SHA-256 hash.
- **OrganizationScope** - public scoped-authorization contract used by modules that need organization-aware permissions.

---

## Shipped flows

- Create, read, update, soft-delete organizations.
- List the current user's organizations with role, scoped permissions, and permission version.
- List members, change member roles, remove members, and allow self-leave.
- Create, list, revoke, validate, and accept organization invitations.
- Accept organization invitations during Users-owned registration.
- Organization-scoped audit lookup.
- GDPR erasure checks and membership anonymization for deleted users.

---

## Invariants

1. Organizations are application-level collaboration boundaries, not separate databases, schemas, caches, queues, or deployments.
2. Active organizations must have at least one owner. The last owner cannot leave, be removed, or be demoted.
3. Full organization deletion is allowed for owners and is soft-delete in v1.
4. Platform/global admins are never hidden organization members. Platform override is explicit per authorization call.
5. A user with both real membership access and platform override is reported as `ScopedPermission`; `PlatformOverride` means membership was bypassed.
6. Role changes and invitations cannot escalate above the actor's active organization role rank.
7. Organization invitation tokens are stored hashed, are single-use, expire, and require an email match.
8. Pending invitations are unique by organization and normalized email.
9. Commands, queries, persisted references, and integration events use durable `OrganizationId`, not slugs.
10. Retained membership history for erased users clears `UserId`; anonymized rows are no longer queryable as that user.

---

## Access control

Organization permissions live in `Modulith.Modules.Organizations.Contracts/Authorization/OrganizationsPermissions.cs`:

```text
organizations.organizations.read
organizations.organizations.write
organizations.organizations.delete
organizations.members.read
organizations.members.manage
organizations.invitations.manage
organizations.audit.read
organizations.platform.override
```

Use `IScopedAuthorizationService<OrganizationScope>` at the endpoint boundary after resolving `organizationRef`. Endpoints may pass `ScopedAuthorizationOptions.WithPlatformOverride` only when global admin bypass is intended.

Do not add organization-scoped permissions to global JWT claims or `/v1/users/me.permissions`. Clients hydrate scoped permissions through `/v1/organizations/my`.

---

## Route and boundary rules

Organization-scoped routes use:

```text
/v1/organizations/{organizationRef}/...
```

Resolve `organizationRef` with `IOrganizationRefResolver` in the endpoint. Handlers should receive `OrganizationId`.

Other modules may reference only `Modulith.Modules.Organizations.Contracts`. They must not reference this internal project, query Organizations tables, join across schemas, or add cross-schema foreign keys.

---

## Integration events and cross-module contracts

Public contracts live under `Modulith.Modules.Organizations.Contracts`.

Important cross-module interactions:

- Users invokes organization invitation validation and acceptance during registration.
- Users invokes the erasure guard before account deletion.
- Notifications consumes organization invitation events to send invite links.
- Audit consumes organization events and stores organization-scoped audit entries.

Raw invitation tokens may appear in `OrganizationInvitationCreatedV1` so Notifications can build the email. They are marked sensitive, must never be logged, and must not be persisted in Audit payloads.

---

## GDPR behavior

Organization business records may outlive a user account, but personal identity must not.

- Sole owners block account deletion until ownership is transferred or the organization is deleted.
- Non-owner account deletion removes active membership and anonymizes retained membership history.
- Membership anonymization clears `UserId` and `RemovedByUserId`.
- Invitation user references (`InvitedByUserId`, `AcceptedUserId`, `RevokedByUserId`) are cleared when they point to the erased user.
- Audit payloads must not include email addresses, display names, raw tokens, or organization names/slugs.

---

## Configuration

Options are bound from `Modules:Organizations` through `OrganizationsOptions`.

- `InvitationLifetimeDays` controls organization invitation expiry and defaults to 14 days.

Use `IOptions<OrganizationsOptions>`. Do not inject raw `IConfiguration` outside module registration.

---

## Known footguns

- Do not compare or authorize by slug after endpoint resolution. Slugs are user-facing references; `OrganizationId` is the durable identity.
- Do not model platform override as membership. It must not affect member lists, owner counts, notifications, or membership history.
- Do not grant owner-level actions through generic write/manage permissions. Deletion uses `organizations.organizations.delete`, and role rank checks protect role changes and invitations.
- Do not discard `ErrorOr` results from cross-module invitation acceptance. Registration must fail or compensate if membership creation fails.
- Do not put raw invitation tokens or invited emails in audit payloads.
- Do not reintroduce non-null `OrganizationMembership.UserId`; GDPR erasure depends on clearing it for retained history.
- Wolverine handlers and subscribers must be public and registered in `OrganizationsModule.AddOrganizationsHandlers`.
- Integration tests that track organization events may need migrated Audit, Notifications, Catalog, and Users schemas because subscribers in those modules react to the same events.
