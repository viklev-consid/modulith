# Deploy to Coolify

This deployment runs the API, migration service, PostgreSQL, and Redis as one Docker
Compose stack. Coolify terminates TLS and routes `modulith-api.intraktiv.com` to the API.

## Coolify setup

1. Create a Docker Compose application from this repository.
2. Set the Compose file to `compose.coolify.yaml`.
3. Add the environment variables from `.env.coolify.example`, replacing every placeholder.
4. Assign `https://modulith-api.intraktiv.com` to the `api` service on port `8080`.
5. Configure the API health check path as `/alive`.
6. Point the `modulith-api.intraktiv.com` DNS record at the Coolify server.

Do not expose the `db`, `cache`, `migrations`, or `volume-permissions` services publicly.
Do not add a host `ports:` mapping for `api`. Coolify's proxy should reach its internal
port `8080` through the Compose network. The API trusts forwarded headers from that
network, so publishing the container port directly would allow clients to spoof them.

The stack runs `migrations` after PostgreSQL becomes healthy. It also runs a one-shot
`volume-permissions` initializer so the non-root API user can write its mounted state.
The API starts only after both one-shot services complete successfully and Redis is
healthy.

Use a strong `POSTGRES_PASSWORD` without semicolons, quotes, or line breaks. The Compose
file interpolates it into an Npgsql connection string.

## Persistent state

Back up these named volumes:

| Volume | Purpose |
| --- | --- |
| `postgres-data` | Business data, TickerQ state, and Wolverine durable messages |
| `blob-data` | User-uploaded content such as avatars |
| `dataprotection-keys` | Keys used to decrypt existing TOTP secrets |
| `redis-data` | Cache persistence; useful but not authoritative |

Losing `dataprotection-keys` prevents users with TOTP enabled from signing in. Losing
`blob-data` removes uploaded files. PostgreSQL is the critical database backup.

## SMTP

The example environment uses STARTTLS on port `587`. For implicit TLS, commonly port
`465`, set:

```dotenv
SMTP_PORT=465
SMTP_USE_STARTTLS=false
SMTP_USE_SSL=true
```

Use a verified sender address for `SMTP_DEFAULT_FROM`.

To bootstrap the first platform admin, set `ADMIN_BOOTSTRAP_ENABLED=true` and add
`Modules__Users__AdminBootstrap__Email=<email>` to the `api` environment. Leave bootstrap
disabled after the first admin has been promoted. Enabling bootstrap without a valid
email intentionally fails API startup.

## Aspire Compose output

The AppHost includes Aspire's Docker Compose publishing environment. Generate Aspire's
model-derived output with:

```bash
aspire publish --project src/AppHost
```

Use that output to inspect drift between local orchestration and production. Coolify
should deploy the checked-in `compose.coolify.yaml`, which intentionally excludes local
Mailpit and pgAdmin and adds production secrets, durable volumes, and health checks.

`compose.coolify.yaml` contains Coolify's `exclude_from_hc` extension for its one-shot
services. Stock `docker compose` does not recognize that extension. For a local
production-shaped Compose check, temporarily omit those lines. For ordinary local
development, continue to run the AppHost.

## Scaling note

Run one API replica initially. Notification server-sent event subscriptions and rate
limits are process-local. Blob storage uses a Docker volume. Horizontal scaling requires
shared blob storage and a cross-instance notification fanout design.
