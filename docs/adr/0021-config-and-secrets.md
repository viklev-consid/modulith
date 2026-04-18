# ADR-0021: Strongly-Typed Options and Hierarchical Configuration

## Status

Accepted

## Context

Configuration in ASP.NET Core is flexible — too flexible, if undisciplined. `IConfiguration["Some:Key"]` scattered through code is common and pernicious:

- No compile-time checking
- No validation
- Drift between config file and code
- No obvious place to document defaults
- Hard to test

The correct pattern is strongly-typed `IOptions<T>` with validation. The question for a template is how strictly to enforce this, and how to handle secrets specifically.

Secrets management has a parallel concern: development uses User Secrets, production uses a secret store. `IConfiguration` is already the abstraction — swap the provider, and consuming code doesn't know the difference. The template should lean into this rather than reinvent.

## Decision

### Strongly-typed Options everywhere

No raw `IConfiguration` injection outside of registration code. Enforced by architectural tests (ADR-0015).

Each module defines an `Options` type and binds in its registration:

```csharp
public sealed class OrdersOptions
{
    [Required] public required string BlobContainer { get; init; }
    [Range(1, 1000)] public int MaxItemsPerOrder { get; init; } = 100;
}

// In OrdersModule.cs
services
    .AddOptions<OrdersOptions>()
    .Bind(configuration.GetSection("Modules:Orders"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

`ValidateOnStart()` is critical — misconfigured deployments fail at boot with a specific message, not on first request with a cryptic NRE.

### Config hierarchy

```
appsettings.json              # committed, defaults and non-secret config
appsettings.{Env}.json        # committed, environment overrides
appsettings.Local.json        # GITIGNORED, developer overrides for non-secret values
User Secrets                  # dev secrets (dotnet user-secrets)
Environment variables         # deployment overrides
External secret store         # prod secrets (Key Vault, Secrets Manager, Vault, K8s)
```

Later sources override earlier. The template's `Program.cs` wires this stack explicitly and includes a commented stub for the external secret provider.

### Module-owned config sections

Each module reads from `Modules:{ModuleName}:*`. Shared infrastructure reads from top-level (`Serilog`, `OpenTelemetry`) or from `Infrastructure:*`. This prevents the flat-config anti-pattern where every section races for the root namespace.

### Aspire parameters for orchestrated resources

For resources Aspire provisions (Postgres connection string, Redis connection, SMTP credentials), use Aspire's parameter model:

```csharp
// AppHost
var dbPassword = builder.AddParameter("db-password", secret: true);
var db = builder.AddPostgres("db", password: dbPassword);

// API
// Connection string auto-wired via ServiceDefaults
builder.AddConnectionString("db");
```

Aspire prompts for values in dev (and persists to user-secrets); production pulls from the configured source.

### Secrets specifically

- **Dev**: User Secrets. `UserSecretsId` in `Api.csproj`. Standard `dotnet user-secrets set Key Value` commands documented in README.
- **Prod**: external provider. Templates does not prescribe — Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, Kubernetes mounted files all work via `IConfiguration` providers. `CONFIG.md` documents the wiring for each.
- **Never**: hard-coded secrets, secrets in `appsettings.json`, encrypted-blob-in-code with key-in-code patterns.

### JWT signing keys

- **Dev**: symmetric key, auto-generated on first run, persisted to user-secrets.
- **Prod**: asymmetric (RSA or ECDSA) recommended — documented. Allows multiple verifiers without sharing signing authority.
- **Rotation**: not implemented. An `ISigningKeyProvider` seam exists; rotation implementations are documented as extension points.

### What NOT to do (anti-patterns)

- Don't encrypt `appsettings.json` fields and ship decryption keys in code.
- Don't use `DataProtection` API for arbitrary secrets — it's for ASP.NET-internal things (cookies, antiforgery), not general-purpose secret storage.
- Don't write secrets to logs. Serilog destructuring (ADR-0010) handles classified properties; also add `[Secret]` to Options sensitive properties as a belt-and-braces measure.
- Don't commit `appsettings.Local.json`. It must be in `.gitignore`.

## Consequences

**Positive:**

- Typed, validated config — misconfigurations fail at startup with clear messages.
- Secret provider is swappable without code changes — `IConfiguration` is the abstraction.
- Aspire parameters give first-class dev ergonomics with prod-ready wiring.
- Module-owned sections scale cleanly as modules multiply.
- Architectural test catches raw `IConfiguration` misuse.

**Negative:**

- More boilerplate — every module has an `Options` class. Accepted.
- `ValidateOnStart` + `ValidateDataAnnotations` can fail obscurely if the validator is wrong; one-time learning curve.
- External provider wiring is per-cloud — template documents but doesn't ship the code. Teams have to write the ~10-line registration for their provider.

## Related

- ADR-0010 (Serilog): masks secret-classified properties.
- ADR-0015 (Architectural Tests): enforces the IConfiguration-only-in-registration rule.
- ADR-0019 (Feature Flags): static flags follow this same Options pattern.
