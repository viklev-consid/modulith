# How-to: Wire Azure Key Vault (secrets + JWT signing)

**Capability:** Secrets management
**Provider:** Azure Key Vault
**Provisioning:** Aspire (`azd`-managed)
**Scope:** Replaces local config secrets with Key Vault in non-dev environments, and flips `ISigningKeyProvider` from the symmetric dev key to a Key Vault-backed asymmetric key.

---

## Goal

After completing this how-to:

- Secrets resolve from Key Vault in `Staging`/`Production`, layered into the existing config hierarchy (above env vars, below external overrides).
- `ISigningKeyProvider` returns an asymmetric RSA key fetched from Key Vault; JWT validation uses the matching public key.
- `Development` is unchanged — still uses `appsettings.Development.json` + user-secrets + the symmetric dev key. No Azure dependency locally.
- `azd up` provisions the vault, grants the app's managed identity `Key Vault Secrets User` + `Key Vault Crypto User`, and seeds the signing key.

## Prerequisites

- Template cloned, builds clean, `ValidateOnStart` green.
- Azure subscription with permission to create resource groups.
- `azd` CLI installed (`winget install microsoft.azd` or equivalent).
- Aspire AppHost targets a cloud-capable environment (already true in the template).

## Non-goals

- Secret rotation with hot reload. This how-to uses **restart-on-rotation** — after updating a secret, restart the app (or trigger a container revision). Polling and Event Grid push are documented as extensions at the bottom.
- Centrally-managed / pre-existing vaults not provisioned by Aspire. See the variant section at the end.
- Certificate management (only secrets + keys).

---

## Steps

### 1. Add packages

Append to `Directory.Packages.props`:

```xml
<PackageVersion Include="Aspire.Hosting.Azure.KeyVault" Version="<aspire-version>" />
<PackageVersion Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="<latest>" />
<PackageVersion Include="Azure.Security.KeyVault.Keys" Version="<latest>" />
<PackageVersion Include="Azure.Identity" Version="<latest>" />
```

Reference them:

- `AppHost.csproj` → `Aspire.Hosting.Azure.KeyVault`
- `Shared.Infrastructure.csproj` → the other three

### 2. Provision the vault in AppHost

In `AppHost/Program.cs`:

```csharp
var keyVault = builder.AddAzureKeyVault("secrets");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(keyVault)
    .WaitFor(keyVault);
```

`WithReference` injects the vault URI as the connection string `ConnectionStrings__secrets` and grants the API's managed identity the `Key Vault Secrets User` role automatically.

For the signing key, add the Crypto User role explicitly via an infra hook — Aspire doesn't grant it by default:

```csharp
keyVault.WithRoleAssignments(api, KeyVaultBuiltInRole.KeyVaultCryptoUser);
```

> **Local dev:** Aspire skips provisioning in `Development` — the vault is only created on `azd up`. Keep the `Development` path off Key Vault entirely (see step 4).

### 3. Register the config source

In `Shared.Infrastructure/Configuration/KeyVaultConfigurationExtensions.cs` (new file):

```csharp
public static class KeyVaultConfigurationExtensions
{
    public static IHostApplicationBuilder AddKeyVaultSecrets(this IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
            return builder; // Dev uses user-secrets; never touches Azure.

        var vaultUri = builder.Configuration.GetConnectionString("secrets")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:secrets is required outside Development. "
                + "Ensure AppHost references the Key Vault resource.");

        builder.Configuration.AddAzureKeyVault(
            new Uri(vaultUri),
            new DefaultAzureCredential(),
            new AzureKeyVaultConfigurationOptions
            {
                // Reload: false — restart-on-rotation (see ADR-xxxx).
                ReloadInterval = null
            });

        return builder;
    }
}
```

Call it in `Api/Program.cs` **before** any `ValidateOnStart` options bind:

```csharp
builder.AddKeyVaultSecrets();
```

### 4. Secret naming convention

Key Vault secret names map to config keys with `--` as the section separator (Azure SDK convention):

| Config key                         | Vault secret name                  |
|------------------------------------|------------------------------------|
| `ConnectionStrings:Postgres`       | `ConnectionStrings--Postgres`      |
| `Modules:Users:JwtIssuer`          | `Modules--Users--JwtIssuer`        |
| `Modules:Notifications:SendGridKey`| `Modules--Notifications--SendGridKey` |

No code changes needed — existing strongly-typed `Options` classes bind to the same paths regardless of source. `ValidateOnStart` will fail fast if a required secret is missing in the vault.

### 5. Flip `ISigningKeyProvider` to Key Vault

The template ships with `SymmetricDevSigningKeyProvider` for `Development`. Add a Key Vault-backed implementation.

`Shared.Infrastructure/Auth/KeyVaultSigningKeyProvider.cs` (new file):

```csharp
internal sealed class KeyVaultSigningKeyProvider : ISigningKeyProvider
{
    private readonly KeyClient _keyClient;
    private readonly string _keyName;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SigningKeyMaterial? _cached;

    public KeyVaultSigningKeyProvider(KeyClient keyClient, IOptions<KeyVaultSigningOptions> options)
    {
        _keyClient = keyClient;
        _keyName = options.Value.SigningKeyName;
    }

    public async ValueTask<SigningKeyMaterial> GetCurrentAsync(CancellationToken ct)
    {
        if (_cached is not null) return _cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is not null) return _cached;

            var key = await _keyClient.GetKeyAsync(_keyName, cancellationToken: ct);
            var rsa = key.Value.Key.ToRSA();

            _cached = new SigningKeyMaterial(
                KeyId: key.Value.Properties.Version,
                Algorithm: SecurityAlgorithms.RsaSha256,
                SigningCredentials: new SigningCredentials(
                    new RsaSecurityKey(rsa) { KeyId = key.Value.Properties.Version },
                    SecurityAlgorithms.RsaSha256),
                PublicKey: new RsaSecurityKey(rsa.ExportParameters(false)));

            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }
}
```

Registration in `Shared.Infrastructure/DependencyInjection.cs`:

```csharp
if (builder.Environment.IsDevelopment())
{
    services.AddSingleton<ISigningKeyProvider, SymmetricDevSigningKeyProvider>();
}
else
{
    services.AddOptions<KeyVaultSigningOptions>()
        .Bind(config.GetSection("Auth:Signing"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddSingleton(sp =>
    {
        var vaultUri = config.GetConnectionString("secrets")!;
        return new KeyClient(new Uri(vaultUri), new DefaultAzureCredential());
    });

    services.AddSingleton<ISigningKeyProvider, KeyVaultSigningKeyProvider>();
}
```

`KeyVaultSigningOptions`:

```csharp
public sealed class KeyVaultSigningOptions
{
    [Required] public string SigningKeyName { get; init; } = default!;
}
```

Add to `appsettings.json`:

```json
"Auth": {
  "Signing": {
    "SigningKeyName": "jwt-signing"
  }
}
```

### 6. Create the signing key

Signing keys are provisioned out-of-band (not from config) because they shouldn't round-trip through source control or deployment variables. One-time setup per environment:

```bash
az keyvault key create \
  --vault-name <vault-name> \
  --name jwt-signing \
  --kty RSA \
  --size 2048 \
  --ops sign verify
```

`azd up` output prints the vault name. Document this in your runbook.

### 7. Verify

1. `dotnet run --project AppHost` locally — confirm the app starts, no Azure calls, JWT issuance still works with the dev symmetric key.
2. `azd up` to a staging env.
3. Hit the auth endpoint, decode the JWT at `jwt.io`, confirm `alg: RS256` and `kid` matches the Key Vault key version.
4. `GET /.well-known/openid-configuration/jwks` returns the RSA public key.
5. Kill a required secret in the vault and restart — app should fail at `ValidateOnStart` with a clear message.

### 8. Write the ADR

Create `docs/adr/NNNN-azure-key-vault-for-secrets-and-signing.md` (Nygard format). Key points to capture:

- Why Key Vault over alternatives (AWS Secrets Manager, HashiCorp Vault, env vars only).
- Why `DefaultAzureCredential` despite the slower credential chain (dev ergonomics, one codepath).
- Why restart-on-rotation vs. polling (simplicity; rotation is rare; restart is a first-class operation in container platforms).
- Why symmetric-in-dev, asymmetric-in-prod (no Azure dep for local runs; prod security posture).

### 9. Update agent docs

- `CLAUDE.md` at root: add a "Secrets" section pointing to this how-to, note the dev/prod split.
- `Shared.Infrastructure/CLAUDE.md`: document `ISigningKeyProvider` has two impls and which is wired when.

---

## Footguns

- **Secret name separator.** Azure uses `--`, not `:` or `__`. Getting this wrong produces silent misses — the key just isn't found and `Options` validation fails with a confusing "required" error rather than "vault miss."
- **`DefaultAzureCredential` chain order.** Locally it'll try Visual Studio, then Azure CLI, then managed identity. If a stale VS login exists, it'll use that and you'll get 403s against a different tenant. `az login` + `az account set` fixes it; consider setting `AZURE_TOKEN_CREDENTIALS` to force a specific type.
- **`WaitFor(keyVault)` matters.** Without it, the API starts before the role assignment propagates and the first config load 403s. Aspire usually handles this, but confirm after template upgrades.
- **Crypto User is separate from Secrets User.** `WithReference` only grants the secrets role. The explicit `WithRoleAssignments` call for crypto is required or signing will 403 at first token issuance, not at startup.
- **Caching the signing key forever.** The provider caches the key in-process for the lifetime of the app. That's intentional given restart-on-rotation, but make sure deployment pipelines actually restart pods on secret updates.
- **Don't put the signing key in the secrets map.** Keys live in the `keys` collection, not `secrets`. The provider uses `KeyClient`, not the config provider.

---

## Extensions

- **Polling reload.** Set `ReloadInterval = TimeSpan.FromMinutes(5)` on `AzureKeyVaultConfigurationOptions`. Note this doesn't refresh `IOptions<T>` that were bound at startup — only `IOptionsMonitor<T>` consumers see updates.
- **Event Grid push reload.** Subscribe a webhook to `Microsoft.KeyVault.SecretNewVersionCreated` and call `IConfigurationRoot.Reload()`. Requires ingress and a shared secret for webhook validation.
- **Key rotation without restart.** Extend `KeyVaultSigningKeyProvider` to hold current + previous keys, refresh on a timer, and expose both via JWKS so in-flight tokens stay valid through rotation.
- **Multiple vaults** (e.g., per-module isolation). Register multiple named `KeyClient`/config sources; prefix secret names with the module. Probably overkill — revisit only if compliance requires it.

---

## Variant: Connecting to a pre-existing vault

If your org provisions Key Vault centrally and the app only consumes it:

1. Skip step 2 (AppHost provisioning).
2. Supply the vault URI via config: `ConnectionStrings:secrets` set from env var, Aspire parameter, or infra pipeline.
3. Role assignments happen out-of-band — coordinate with whoever owns the vault to grant `Key Vault Secrets User` and `Key Vault Crypto User` to the app's managed identity / service principal.
4. Everything from step 3 onward works unchanged.
