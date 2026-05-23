# Manage Organization Invitations

Organization invitations invite an email address into one organization with one requested organization role.

They are separate from global account invitations. The global user role created through an organization invite is still `user` unless a platform admin changes it.

Invitation lifetime is configured through `Modules:Organizations:InvitationLifetimeDays`.

## Flow

1. An organization owner/admin creates an invitation for an email and role.
2. The raw token is returned to the caller and sent through Notifications.
3. If the email belongs to an existing user, accepting the token creates membership.
4. If the email does not belong to a user, the client sends the invite token through the Users-owned registration flow.
5. Users creates the account, then the organization invite is consumed and membership is created.

Organizations owns membership. Users owns account creation.

The default frontend link for organization invitations is:

```text
/invite?token={rawToken}&email={email}
```

Signed-in clients accept with `POST /v1/organizations/invitations/accept`. Signed-out clients should route through registration using `organizationInvitationToken`. If registration cannot consume the organization invitation, the request fails and the newly created user record is compensated instead of silently returning a user without membership.

## Invariants

- Tokens are stored as hashes.
- Raw tokens are marked sensitive, omitted from audit payloads, and should only be used for the invite link or the one-time HTTP response.
- Pending invites are unique by organization and email.
- Accepted, revoked, expired, or deleted-organization invites cannot be accepted.
- Invite acceptance requires the accepting/registered email to match the invitation email.
- The requested role must be a known organization role.
- The requested role cannot outrank the inviter's active organization role.
- A deleted organization cannot issue or accept invites.

## Authorization

Creating, listing, and revoking invitations requires:

```text
organizations.invitations.manage
```

Platform override may be allowed for operator endpoints, but must be explicit.
