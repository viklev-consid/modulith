# Global vs Scoped Authorization

Modulith has two authorization layers.

## Global RBAC

Global RBAC answers questions about platform-level capabilities:

```text
Can this user list all users?
Can this user change global roles?
Can this user use platform override?
```

Global permissions come from the user's global role and are exposed through `/v1/users/me`.

Use global permissions for platform administration and module capabilities that are not tied to an organization.

## Scoped authorization

Scoped authorization answers questions about one resource scope:

```text
Can this user manage members in this organization?
Can this user create a project in this organization?
Can this user read audit entries for this organization?
```

Scoped permissions are evaluated per request and are not stored in JWTs.

Use scoped authorization when the answer can differ by organization.

## Platform override

Platform override lets a global admin bypass membership checks for explicit operator/support actions.

Rules:

- disabled by default
- enabled per endpoint/policy call
- audited as platform override
- does not create implicit organization membership

Prefer scoped membership access for normal product workflows.
