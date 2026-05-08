# How-to: Wire Azure Key Vault for Secrets

**Capability:** Secrets management  
**Provider:** Azure Key Vault  
**Provisioning:** Aspire / Azure deployment  
**Scope:** Adds Key Vault as a configuration provider in non-development environments. JWT signing remains the template's current symmetric `Jwt:SigningKey` unless you implement an asymmetric signing extension.

---

## Goal

After completing this how-to:

- Secrets resolve from Key Vault in `Staging`/`Production`, layered into the existing configuration system before options validation runs.
- Existing strongly-typed options keep binding from the same keys, such as `Jwt:SigningKey` and `Modules:Notifications:Smtp:Password`.
- `Development` remains unchanged: `appsettings.Development.json`, user-secrets, Aspire-provided resources, and no Azure dependency.
- `azd up` or your infrastructure pipeline provisions the vault and grants the app identity secret-read access.

## Non-goals

- JWT asymmetric signing. The current template signs and validates JWTs directly from `JwtOptions.SigningKey` using a symmetric key.
- Secret hot reload. This guide assumes restart-on-rotation.
- Centrally-managed vault governance. The variant at the end covers consuming a pre-existing vault.

---

## Steps

### 1. Add packages

Add the package versions your project uses for Azure integration, then reference them from the API or shared infrastructure project that will register the provider:

```xml
<PackageVersion Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="<latest>" />
<PackageVersion Include="Azure.Identity" Version="<latest>" />
```

If Aspire provisions the vault, also add the matching Aspire Key Vault hosting package to `AppHost`.

### 2. Provision or reference the vault

If Aspire owns the Azure resource, add a Key Vault resource in `src/AppHost/AppHost.cs` and reference it from the API project. The exact API can vary by Aspire version, so follow the current Aspire Azure Key Vault documentation for the package version in `Directory.Packages.props`.

The important runtime contract is that the API receives a vault URI, commonly as a connection string named `secrets`:

```csharp
var vaultUri = builder.Configuration.GetConnectionString("secrets");
```

Grant the API's managed identity permission to read secrets, for example `Key Vault Secrets User`.

### 3. Register the config source

Add this before any `AddOptions(...).Bind(...).ValidateOnStart()` calls in `src/Api/Program.cs` or in a shared registration extension called from there:

```csharp
if (!builder.Environment.IsDevelopment())
{
    var vaultUri = builder.Configuration.GetConnectionString("secrets")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:secrets is required outside Development.");

    builder.Configuration.AddAzureKeyVault(
        new Uri(vaultUri),
        new DefaultAzureCredential(),
        new AzureKeyVaultConfigurationOptions
        {
            ReloadInterval = null
        });
}
```

This keeps local development off Azure and lets `ValidateOnStart()` fail fast when a required production secret is missing.

### 4. Name secrets for existing config keys

Key Vault secret names map to configuration keys with `--` as the section separator:

| Config key | Vault secret name |
|---|---|
| `Jwt:SigningKey` | `Jwt--SigningKey` |
| `ConnectionStrings:db` | `ConnectionStrings--db` |
| `Modules:Notifications:Smtp:Password` | `Modules--Notifications--Smtp--Password` |
| `Modules:Users:Google:ClientSecret` | `Modules--Users--Google--ClientSecret` |

No consumer code changes are needed. Options still bind from the same configuration paths.

### 5. Verify

1. Run locally with `dotnet run --project src/AppHost`; development should not call Azure.
2. Deploy to staging with the vault URI configured.
3. Store `Jwt--SigningKey` with at least 32 characters and any other required secrets.
4. Start the API and confirm `ValidateOnStart()` succeeds.
5. Remove a required staging secret and restart; the app should fail during startup with an options validation error.

---

## JWT Signing Notes

The template currently uses symmetric HMAC signing:

- `src/Shared/Modulith.Shared.Infrastructure/Auth/JwtOptions.cs` requires `Issuer`, `Audience`, and `SigningKey`.
- `src/Api/Program.cs` validates bearer tokens with a `SymmetricSecurityKey` built from `Jwt:SigningKey`.
- `src/Modules/Users/.../Security/JwtGenerator.cs` signs access tokens with the same key.

To move to RSA/ECDSA signing, first introduce a signing-key abstraction and update both issuance and validation. A Key Vault `KeyClient`-backed signer, JWKS endpoint, current/previous key rotation, and `kid` handling are extension work, not shipped behavior.

---

## Footguns

- **Secret name separator.** Azure uses `--`, not `:` or `__`.
- **Provider order matters.** Register Key Vault before options bind and validate.
- **Managed identity permissions.** Secret read permission is enough for symmetric config secrets; crypto permissions are only needed if you later implement Key Vault-backed signing.
- **No local Azure dependency.** Keep the provider disabled in `Development`.
- **No secrets in `appsettings.json`.** Committed JSON files hold non-secret defaults only.

---

## Variant: Connecting to a Pre-existing Vault

If your organization provisions Key Vault centrally:

1. Skip Aspire provisioning.
2. Supply the vault URI as `ConnectionStrings:secrets` through environment variables or deployment configuration.
3. Have the vault owner grant the app identity `Key Vault Secrets User`.
4. Use the same provider registration and secret naming conventions above.
