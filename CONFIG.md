# CONFIG.md

Configuration and secret management in Modulith. For the decisions, see [`docs/adr/0021-config-and-secrets.md`](docs/adr/0021-config-and-secrets.md).

---

## Principles

1. **Every module binds its config to a strongly-typed `Options` class with validation.**
2. **No raw `IConfiguration` injection outside registration code.** Enforced by architectural tests.
3. **Secrets never in `appsettings.json`.** Use User Secrets in dev, external secret stores in prod.
4. **`ValidateOnStart()` on all options.** Misconfiguration fails at boot, not on first request.
5. **Aspire parameters for resources Aspire orchestrates.** Native dev ergonomics + prod-ready wiring.

---

## Configuration hierarchy

Sources, in order (later overrides earlier):

```
1. appsettings.json               (committed; defaults + non-secret config)
2. appsettings.{Environment}.json (committed; per-environment overrides)
3. User Secrets                   (development secrets; dotnet user-secrets)
4. Environment variables          (deployment-layer configuration)
5. External secret store          (production secrets; Key Vault, etc., when wired by the app)
```

### appsettings.json

Committed. Contains:

- Non-secret defaults for every option.
- Feature flag defaults.
- Module enable/disable defaults.
- Logging configuration (minimum levels, sink setup).

**Never** contains: connection strings with passwords, API keys, JWT signing keys, SMTP credentials, anything that could be a secret.

### appsettings.{Environment}.json

Committed. Per-environment overrides. `appsettings.Development.json`, `appsettings.Staging.json`, `appsettings.Production.json`.

Environment detection via `ASPNETCORE_ENVIRONMENT`. In Aspire dev, this defaults to `Development`.

### User Secrets

Dev-only. Scoped to the API project via `<UserSecretsId>` in `Api.csproj`.

```bash
cd src/Api
dotnet user-secrets set "ConnectionStrings:db" "Host=localhost;..."
dotnet user-secrets set "Jwt:SigningKey" "..."
dotnet user-secrets list
```

Stored outside the repo (at `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` or `~/.microsoft/usersecrets/<id>/secrets.json`).

### Environment variables

Standard ASP.NET Core binding: `Modules__Orders__BlobContainer=orders-dev` maps to `Modules:Orders:BlobContainer` (`__` replaces `:`).

Use for:
- Deployment-time overrides (container orchestration)
- CI-specific values
- Any ephemeral override

### External secret store (production)

The template **does not prescribe or pre-wire** a secret store. It documents integration for:

- **Azure Key Vault** via `Azure.Extensions.AspNetCore.Configuration.Secrets`
- **AWS Secrets Manager** via `Kralizek.Extensions.Configuration.AWSSecretsManager`
- **HashiCorp Vault** via `VaultSharp` or community providers
- **Kubernetes mounted secrets** via `AddKeyPerFile`

Wiring example (Azure Key Vault, to add near the top of `Api/Program.cs` before options bind):

```csharp
if (builder.Environment.IsProduction())
{
    var vaultUri = builder.Configuration["Azure:KeyVault:Uri"];
    if (!string.IsNullOrWhiteSpace(vaultUri))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(vaultUri),
            new DefaultAzureCredential());
    }
}
```

Teams add the provider package, register the provider, and keep strongly-typed options unchanged.

---

## Strongly-typed Options

Every module defines an `Options` type:

```csharp
public sealed class OrdersOptions
{
    [Required]
    public required string BlobContainer { get; init; }

    [Range(1, 10000)]
    public int MaxItemsPerOrder { get; init; } = 100;

    [Url]
    public string? WebhookUrl { get; init; }
}
```

Registered in the module's `AddXxxModule`:

```csharp
services.AddOptions<OrdersOptions>()
    .Bind(configuration.GetSection("Modules:Orders"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**`ValidateOnStart()`** is critical — misconfigured deployments fail at boot with a specific message.

Consumed via `IOptions<T>`:

```csharp
public sealed class Handler(IOptions<OrdersOptions> options)
{
    private readonly OrdersOptions _opts = options.Value;
    // ...
}
```

### Arch test enforcement

Architectural test forbids `IConfiguration` injection outside types named `*Module` or `Program`. Caught at PR time, not runtime.

---

## Module-owned config sections

| Section | Owner |
|---|---|
| `Modules:<n>:*` | That module |
| `ConnectionStrings:<n>` | Infrastructure |
| `Serilog` | Logging |
| `OpenTelemetry` | Observability |
| `FeatureManagement` | Feature flags |
| `Jwt` | Auth |
| `Blob` | Shared.Infrastructure |
| `Notifications:Smtp` | Notifications module transport |

Modules own `Modules:<n>`. Sharing that namespace across modules is a design smell.

---

## Aspire parameters

For resources Aspire provisions, use its parameter model:

```csharp
// AppHost/Program.cs
var dbPassword = builder.AddParameter("db-password", secret: true);
var db = builder.AddPostgres("db", password: dbPassword);

var redis = builder.AddRedis("cache");

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
    .WithEndpoint(targetPort: 1025, name: "smtp")
    .WithEndpoint(targetPort: 8025, name: "ui");

builder.AddProject<Projects.Modulith_Api>("api")
    .WithReference(db)
    .WithReference(redis)
    .WithEnvironment("Notifications__Smtp__Host", mailpit.GetEndpoint("smtp"));
```

In dev, Aspire prompts for parameter values and persists them to user-secrets. In prod, values come from the configured source.

---

## JWT keys

### Development

Symmetric key supplied through configuration, normally User Secrets in development:

```
Jwt:Issuer = modulith-dev
Jwt:Audience = modulith-dev
Jwt:SigningKey = <at least 32 characters>
```

`JwtOptions.SigningKey` is `[Required]` and `[MinLength(32)]`, so startup fails fast if the key is missing or too short.

### Production

The current template uses the same symmetric `Jwt:SigningKey` option for signing and validation. Store it in your production secret store and make it at least 32 characters:

Configuration:

```
Jwt:Issuer = <issuer>
Jwt:Audience = <audience>
Jwt:SigningKey = <secret, at least 32 characters>
```

Asymmetric signing and key rotation are not implemented in the template. If your production posture requires RSA/ECDSA keys, introduce a signing-key abstraction, update both `JwtGenerator` and JWT bearer validation, and document the rotation procedure in your ops runbook.

### What NOT to do

- Don't hardcode keys.
- Don't commit any `*.pem` files.
- Don't log keys (Serilog destructuring masks properties named `*Key`, `*Secret`, but belt-and-braces).
- Don't use `DataProtection` for JWT signing — that's for cookies and antiforgery.

---

## Connection strings

Aspire handles the DB connection string in dev:

```csharp
// Api/Program.cs
builder.AddNpgsqlDbContext<...>("db");
```

In prod, the connection string comes from secret store:

```
ConnectionStrings:db = Host=prod-db;Database=modulith;Username=...;Password=...
```

(`Password=` reads from secret store; the rest from non-secret config.)

---

## SMTP credentials (Notifications)

Dev: Mailpit via Aspire. No credentials.

Prod: provider-specific. Example for SMTP:

```
Notifications:Smtp:Host = smtp.provider.com
Notifications:Smtp:Port = 587
Notifications:Smtp:Username = <secret>
Notifications:Smtp:Password = <secret>
```

Or API-based (SendGrid, SES): override `IEmailSender` registration in prod to use a provider-specific implementation.

---

## Feature flags

See [`docs/how-to/use-feature-flags.md`](docs/how-to/use-feature-flags.md) for full detail.

Startup flags (`IOptions`) vs. runtime flags (`IFeatureManager`) use different config shapes.

---

## Logging

Serilog config in `appsettings.json`:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.OpenTelemetry" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "OpenTelemetry",
        "Args": {
          "Endpoint": "http://localhost:4317",
          "Protocol": "Grpc"
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithEnvironmentName", "WithSpan" ]
  }
}
```

Per-environment overrides in `appsettings.{Environment}.json`. OTLP endpoint differs per environment.

---

## Common mistakes

- **Expecting `appsettings.Local.json` to load automatically.** It is gitignored, but the template does not add it as a config provider.
- **Secrets in `appsettings.json`.** Always wrong.
- **Raw `IConfiguration` access.** Arch test catches it; fix by adding an `Options` class.
- **No `ValidateOnStart()`.** Misconfiguration fails at first request with a cryptic NRE instead of at boot.
- **Options without validation attributes.** Silent accepts of bad values.
- **Using environment variables for sensitive values in dev.** Use User Secrets — they're less likely to leak into shell history or logs.
- **Mixing dev and prod config in the same file.** Use `appsettings.{Environment}.json` plus environment-specific secret sources.

---

## Verification checklist

- [ ] Every module has an `Options` class.
- [ ] Every `Options` class has validation attributes + `ValidateOnStart`.
- [ ] No raw `IConfiguration` injection (arch test passing).
- [ ] Local-only settings are supplied through user-secrets or an explicitly registered local config provider.
- [ ] `UserSecretsId` is set in `Api.csproj`.
- [ ] Secret store wiring is configured for production when production secrets should come from a provider.
- [ ] JWT signing keys are not committed.
- [ ] SMTP credentials are in secret store or Aspire parameters.

---

## Related

- [`docs/adr/0021-config-and-secrets.md`](docs/adr/0021-config-and-secrets.md)
- [`docs/adr/0019-feature-flags.md`](docs/adr/0019-feature-flags.md)
- [`docs/adr/0010-serilog-and-otel.md`](docs/adr/0010-serilog-and-otel.md)
- [`docs/how-to/use-feature-flags.md`](docs/how-to/use-feature-flags.md)
