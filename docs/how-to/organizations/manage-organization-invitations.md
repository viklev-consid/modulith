# Manage Organization Invitations

Organization invitations invite an email address into one organization with one requested organization role.

They are separate from global account invitations. The global user role created through an organization invite is still `user` unless a platform admin changes it.

## Flow

1. An organization owner/admin creates an invitation for an email and role.
2. The raw token is returned or sent through Notifications.
3. If the email belongs to an existing user, accepting the token creates membership.
4. If the email does not belong to a user, the client sends the invite token through the Users-owned registration flow.
5. Users creates the account, then the organization invite is consumed and membership is created.

Organizations owns membership. Users owns account creation.

## Invariants

- Tokens are stored as hashes.
- Pending invites are unique by organization and email.
- Accepted, revoked, expired, or deleted-organization invites cannot be accepted.
- Invite acceptance requires the accepting/registered email to match the invitation email.
- The requested role must be a known organization role.
- A deleted organization cannot issue or accept invites.

## Authorization

Creating, listing, and revoking invitations requires:

```text
organizations.invitations.manage
```

Platform override may be allowed for operator endpoints, but must be explicit.
