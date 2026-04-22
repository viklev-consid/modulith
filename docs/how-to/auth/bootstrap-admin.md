# How to bootstrap the first admin account

After deploying to a new environment, no admin users exist. This guide explains how to promote the first admin.

---

## Development

In development (`ASPNETCORE_ENVIRONMENT=Development`), the seeder creates an admin automatically.

Configure the admin's email and display name in `appsettings.Development.json`:

```json
{
  "Modules": {
    "Users": {
      "Dev": {
        "AdminEmail": "admin@example.test",
        "AdminDisplayName": "Admin"
      }
    }
  }
}
```

The seeder runs when the application starts in development mode. If a user with `AdminEmail` already exists, their role is corrected to `Admin` idempotently.

---

## Production / staging

The `AdminBootstrapper` hosted service handles non-development environments. It runs once at startup and does nothing if an admin already exists.

**Step 1 — Register the account**

Call `POST /v1/users/register` with the email you want to promote. The account must exist before the bootstrapper can promote it.

**Step 2 — Enable the bootstrapper**

Set the following in your environment configuration (secrets or environment variables — never in appsettings.json):

```json
{
  "Modules": {
    "Users": {
      "AdminBootstrap": {
        "Enabled": true,
        "Email": "your-admin@yourdomain.com"
      }
    }
  }
}
```

**Step 3 — Restart the application**

On startup, the bootstrapper will:

1. Check if any admin already exists → if yes, do nothing.
2. Look up the user with the configured email → promote to Admin.
3. Log the result.

**Step 4 — Disable the bootstrapper**

Once promoted, set `Enabled: false` (or remove the setting). The bootstrapper is a one-shot tool; there is no reason to leave it enabled.

---

## What happens if the email is not found

The bootstrapper logs a warning and continues. It does **not** create users from scratch. Register the account first, then restart to promote it.

---

## Security note

- `AdminBootstrap:Email` should be stored in a secret store (user-secrets in dev, environment variables or a vault in production). Do not commit it to source control.
- After bootstrapping, the admin can promote additional users via `PUT /v1/users/{userId}/role`.

---

## See also

- [Use RBAC in endpoints and handlers](use-rbac.md)
- ADR-0030: RBAC design rationale
