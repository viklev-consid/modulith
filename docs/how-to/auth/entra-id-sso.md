# How-to: Wire Entra ID as the sole identity provider (workforce SSO)

**Capability:** Authentication & authorization
**Provider:** Microsoft Entra ID (workforce, single-tenant)
**Scope:** Replaces the template's local JWT issuance path with Entra as the sole IdP. Adds JIT user provisioning, Graph-based profile enrichment on first login, and app-role-based authorization with two roles: `Admin` and `User`.

---

## Goal

After completing this how-to:

- Entra is the only IdP. The template's local JWT issuance/login endpoints are removed.
- Access tokens issued by Entra are validated by the API using Entra's published signing keys (JWKS).
- On first login, a `User` aggregate is created from token claims (`oid` as stable key).
- On first login, Microsoft Graph is called once to enrich the profile: photo stored in `IBlobStore`, job title and department cached on the `User`.
- Authorization uses **two app roles** — `Admin` and `User` — defined in the app registration manifest, surfaced as the `roles` claim, enforced via ASP.NET Core policies.
- Entra's "Assignment required" is **on** — users must be explicitly assigned to a role to reach the app. Unassigned users are blocked at the Entra login screen.
- A manual "refresh profile" endpoint re-fetches Graph data on demand.

> **On the role scope.** Intentionally minimal. `Admin` and `User` are about *application capability* — can you administer the app, can you use it at all. Domain-level roles (Manager, HR, etc.) are a separate concern that belongs in an Organization module, sourced from your HR system or Entra org data. Don't add them to the app registration; don't conflate "who you are in the org" with "what the app lets you do."

## Non-goals

- Multi-tenant / B2B guest users. Document as a variant; don't implement.
- External ID / B2C (customer-facing).
- Organization-scoped authorization (reporting chain, data scoping). Deliberately out of scope — see `docs/howto/authorization/` if/when added.
- Entra group claims. Using app roles instead.
- Token issuance. This app is a resource server only; it validates tokens, never issues them.

## Prerequisites

- Template cloned, builds clean.
- Entra tenant with permission to register applications and assign app roles.
- A test user in the tenant (not a guest).

---

## Steps

### 1. Register the app in Entra

Two app registrations — standard pattern for SPA + API:

**API app registration (`YourApp.Api`):**

- Expose an API → set Application ID URI to `api://<api-client-id>`
- Add a scope: `access_as_user` (user consent, "Access YourApp API as the signed-in user")
- Add app roles (Manifest → `appRoles`):
  ```json
  [
    { "id": "<new-guid>", "displayName": "Admin", "value": "Admin",
      "description": "Administers the application",
      "allowedMemberTypes": ["User"], "isEnabled": true },
    { "id": "<new-guid>", "displayName": "User", "value": "User",
      "description": "Standard application user",
      "allowedMemberTypes": ["User"], "isEnabled": true }
  ]
  ```
- API permissions: add **Microsoft Graph → User.Read** (delegated) so the API can call Graph on behalf of the signed-in user.

**SPA/client app registration (`YourApp.Spa`):** register separately for your frontend, grant it the `access_as_user` scope on the API. Not covered further in this how-to — see your SPA framework's Entra guide.

**Enterprise app → Properties → Assignment required: Yes.** This is the gate. Only users explicitly assigned to a role can reach the app. Unassigned users see an Entra error telling them to request access.

**Enterprise app → Users and groups:** assign every test user to either `Admin` or `User`. Assigning to "Default Access" does not produce a `roles` claim and the user will be rejected by the app.

### 2. Remove local auth

Delete the following from the template (they're mutually exclusive with Entra-only):

- Local login/register endpoints in the Users module
- Password hashing infrastructure (`IPasswordHasher`, related services)
- `ISigningKeyProvider` and implementations — no longer issuing tokens
- `/.well-known/openid-configuration/jwks` endpoint — Entra publishes its own
- JWT-issuance-related options (`Auth:Signing:*`)

Keep: the `User` aggregate, the `Users` module, user profile endpoints. The aggregate still exists — it's just populated from Entra claims, not created by a register endpoint.

Update the arch tests to assert no password-related types remain in the codebase — prevents accidental re-introduction.

### 3. Add packages

`Directory.Packages.props`:

```xml
<PackageVersion Include="Microsoft.Identity.Web" Version="<latest>" />
<PackageVersion Include="Microsoft.Identity.Web.MicrosoftGraph" Version="<latest>" />
```

Reference in `Api.csproj` (or wherever auth is composed — adjust to your actual layout):

- `Microsoft.Identity.Web` — token validation
- `Microsoft.Identity.Web.MicrosoftGraph` — Graph client with OBO token acquisition

### 4. Configure token validation

`appsettings.json`:

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "<tenant-guid>",
  "ClientId": "<api-client-id>",
  "Audience": "api://<api-client-id>"
},
"DownstreamApi": {
  "MicrosoftGraph": {
    "BaseUrl": "https://graph.microsoft.com/v1.0",
    "Scopes": "User.Read"
  }
}
```

Strongly-typed options (per template convention — no raw `IConfiguration` outside registration):

```csharp
public sealed class AzureAdOptions
{
    [Required] public string Instance { get; init; } = default!;
    [Required] public string TenantId { get; init; } = default!;
    [Required] public string ClientId { get; init; } = default!;
    [Required] public string Audience { get; init; } = default!;
}
```

Registration in the API composition root:

```csharp
services.AddOptions<AzureAdOptions>()
    .Bind(config.GetSection("AzureAd"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(config.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(config.GetSection("DownstreamApi:MicrosoftGraph"))
    .AddInMemoryTokenCaches();

services.AddAuthorization(options =>
{
    // Default policy: authenticated AND has one of the known app roles.
    // Belt-and-braces — Entra's Assignment Required gate should already
    // reject unassigned users, but this ensures tokens without a roles
    // claim can't reach protected endpoints even if that setting drifts.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("Admin", "User")
        .Build();

    options.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
});
```

`Microsoft.Identity.Web` handles JWKS fetching, caching, and rotation automatically — no manual key management.

### 5. JIT user provisioning

Create a claims-principal-transformer that runs after token validation, ensures a local `User` exists, and enriches on first login.

`Users.Infrastructure/Auth/UserProvisioningClaimsTransformation.cs`:

```csharp
internal sealed class UserProvisioningClaimsTransformation(
    IUserRepository users,
    IUserProvisioningService provisioning,
    ILogger<UserProvisioningClaimsTransformation> logger)
    : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return principal;

        // Guard: only run once per request.
        if (principal.HasClaim(c => c.Type == "app_user_id")) return principal;

        var entraOid = principal.FindFirstValue("oid")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token missing oid claim.");

        var user = await users.FindByEntraOidAsync(entraOid, CancellationToken.None);
        if (user is null)
        {
            user = await provisioning.ProvisionFromPrincipalAsync(principal, CancellationToken.None);
            logger.LogInformation("Provisioned new user {UserId} from Entra oid {Oid}", user.Id, entraOid);
        }

        var identity = (ClaimsIdentity)principal.Identity;
        identity.AddClaim(new Claim("app_user_id", user.Id.ToString()));

        return principal;
    }
}
```

Register:

```csharp
services.AddTransient<IClaimsTransformation, UserProvisioningClaimsTransformation>();
```

`IUserProvisioningService` (Users module contract):

```csharp
public interface IUserProvisioningService
{
    Task<User> ProvisionFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken ct);
    Task RefreshProfileAsync(UserId userId, CancellationToken ct);
}
```

Implementation sketch:

```csharp
internal sealed class UserProvisioningService(
    IUserRepository users,
    GraphServiceClient graph,
    IBlobStore blobs,
    IUnitOfWork uow)
    : IUserProvisioningService
{
    public async Task<User> ProvisionFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var oid = principal.FindFirstValue("oid")!;
        var email = principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue(ClaimTypes.Email)!;
        var displayName = principal.FindFirstValue("name") ?? email;

        var user = User.CreateFromEntra(
            entraOid: oid,
            email: email,
            displayName: displayName);

        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct); // persist before Graph calls; Graph enrichment is best-effort

        await EnrichFromGraphAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        return user;
    }

    public async Task RefreshProfileAsync(UserId userId, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct);
        await EnrichFromGraphAsync(user, ct);
        await uow.SaveChangesAsync(ct);
    }

    private async Task EnrichFromGraphAsync(User user, CancellationToken ct)
    {
        try
        {
            var me = await graph.Me.GetAsync(
                r => r.QueryParameters.Select = new[] { "jobTitle", "department", "officeLocation" },
                ct);

            user.UpdateProfile(
                jobTitle: me?.JobTitle,
                department: me?.Department,
                officeLocation: me?.OfficeLocation);

            await using var photoStream = await graph.Me.Photo.Content.GetAsync(cancellationToken: ct);
            if (photoStream is not null)
            {
                var blobRef = await blobs.PutAsync(
                    container: "user-photos",
                    content: photoStream,
                    contentType: "image/jpeg",
                    ct: ct);
                user.SetPhoto(blobRef);
            }
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // No photo set in Entra — not an error.
        }
        catch (Exception ex)
        {
            // Graph enrichment is best-effort. User is provisioned; profile can be refreshed later.
            // Log via injected ILogger in real impl.
        }
    }
}
```

### 6. The `User` aggregate changes

The aggregate now tracks Entra identity instead of password credentials:

```csharp
public sealed class User : AggregateRoot<UserId>, IAuditableEntity
{
    public string EntraOid { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? JobTitle { get; private set; }
    public string? Department { get; private set; }
    public string? OfficeLocation { get; private set; }
    public BlobRef? PhotoBlob { get; private set; }
    public DateTimeOffset? ProfileRefreshedAt { get; private set; }

    public static User CreateFromEntra(string entraOid, string email, string displayName)
    {
        var user = new User
        {
            Id = UserId.New(),
            EntraOid = entraOid,
            Email = email,
            DisplayName = displayName
        };
        user.Raise(new UserProvisionedFromEntra(user.Id, entraOid, email));
        return user;
    }

    public void UpdateProfile(string? jobTitle, string? department, string? officeLocation)
    {
        JobTitle = jobTitle;
        Department = department;
        OfficeLocation = officeLocation;
        ProfileRefreshedAt = DateTimeOffset.UtcNow;
    }

    public void SetPhoto(BlobRef blobRef) => PhotoBlob = blobRef;
}
```

Add a unique index on `EntraOid` in the EF configuration. `UserProvisionedFromEntra` flows through your existing audit pipeline automatically.

### 7. Applying authorization

Slices use the `Admin` role via the policy registered above:

```csharp
app.MapPost("/api/admin/tenants", AdminTenants.Handle)
    .RequireAuthorization("RequireAdmin");
```

Every other endpoint is protected by the fallback policy — authenticated + has either `Admin` or `User`. No per-endpoint attribute needed for standard user-facing endpoints; they're covered by the default.

For retrieving the current app user inside handlers, extend `ICurrentUser` to expose the `app_user_id` claim as a `UserId`, plus an `IsAdmin` convenience backed by the `roles` claim.

> **Domain-level authorization goes elsewhere.** Checks like "is this user the manager of that employee" or "does this user belong to HR" are not app-role checks. They belong in a domain service in the relevant module (Organization, HR, etc.), called explicitly from handlers and returning `Result.Forbidden()`. Keep the `roles` claim for Admin/User only — don't stuff domain concepts into it.

### 8. Refresh profile endpoint

A minimal slice in the Users module:

```
Users/Features/RefreshMyProfile/
    Command.cs       // empty record, no params
    Handler.cs       // resolves current user, calls IUserProvisioningService.RefreshProfileAsync
    Endpoint.cs      // POST /api/users/me/refresh-profile
```

Consider rate-limiting this endpoint with the `ExpensivePolicy` tier from your rate-limiting setup — Graph calls are expensive and the user doesn't need to hammer it.

### 9. Local development

Entra-only means local dev needs a real Entra tenant. Two approaches:

- **Shared dev tenant** (recommended): a dedicated Entra tenant for development with test users. All devs share it. Document tenant ID + sample user credentials in `docs/local-dev.md`.
- **Per-dev tenant**: each dev uses their own tenant. More isolation, more setup friction.

Aspire parameter for `AzureAd:TenantId` and `AzureAd:ClientId` — these are not secrets but differ per environment. The client secret (if your SPA flow needs one) goes through Aspire + Key Vault (see `docs/howto/secrets/azure-key-vault.md`).

Integration tests: use `WebApplicationFactory` with a custom scheme that forges a `ClaimsPrincipal` carrying the expected claims. Don't hit real Entra in tests. Shared `TestSupport` project should expose `WithFakeUser(oid, roles...)` helper.

### 10. Update compliance docs

`COMPLIANCE.md` changes:

- Remove references to local password storage / hashing.
- Note that authentication is delegated to Entra — password policy, MFA, account lockout, compromise detection all handled by Entra. Not your compliance surface.
- GDPR: `EntraOid` and enriched profile data are personal data. Add `[PersonalData]` attributes per your classification convention. Erasure: the `IPersonalDataEraser` for the Users module must clear local profile fields and delete the photo blob; the Entra account itself is out of your scope to erase.

### 11. Write the ADR

`docs/adr/NNNN-entra-id-as-sole-idp.md`. Key points:

- Why Entra-only (vs. coexistence with local auth): reduces attack surface, offloads password policy/MFA.
- Why two app roles only (`Admin`, `User`): these are *application capability* roles. Domain-level roles (Manager, HR, etc.) belong in a domain module, not in the token.
- Why app roles vs. groups: strongly-typed, decoupled from tenant group structure, no 200-group overflow issue.
- Why Assignment Required is on by default (Shape A): principle of least access; the failure mode (friendly Entra error) is better than "someone got in who shouldn't have." Customers who want tenant-wide access turn it off (see variant).
- Why JIT + first-login Graph enrichment vs. every-login enrichment: keeps Graph off the hot path; staleness acceptable given refresh endpoint.
- Why `oid` as the stable key vs. email: email can change in Entra; `oid` is immutable per user per tenant.
- Out of scope and why: multi-tenant, guest users, org-scoped authorization.

### 12. Update agent docs

- `CLAUDE.md` (root): note "auth is Entra-only, no local login path." Link this how-to.
- `Users/CLAUDE.md`: document `User` aggregate is populated from Entra claims + Graph; `IUserProvisioningService` is the entry point. Note that `Admin` and `User` are the only app roles — adding new *application-capability* roles requires both a manifest change and policy registration, but domain roles (Manager, HR, etc.) do not go here; they belong in the relevant domain module.

---

## Footguns

- **`oid` vs. `sub`.** Use `oid` (object ID, tenant-scoped, stable). `sub` is also stable but is pairwise (per app), which is fine here but `oid` is what every other Entra doc/tool references. Don't use `preferred_username` or email as the key — both can change.
- **"Default Access" is not a role.** When assigning users via the enterprise app's "Users and groups" UI, Entra defaults the role dropdown to "Default Access" which produces no `roles` claim. In Shape A this will cause Entra itself to reject the sign-in as if the user wasn't assigned. Always pick `Admin` or `User` explicitly.
- **Groups claim overflow.** Not an issue here because we're using app roles, but if a future maintainer switches to groups: tokens hard-cap at ~200 group claims and then replace the claim with a Graph URL. If you ever see `hasgroups: true` in a token, that's the signal.
- **Graph photo endpoint returns 404, not empty, when there's no photo.** Catch `ODataError` with status 404 specifically — don't swallow all Graph errors or you'll hide real issues.
- **OBO flow requires the `access_as_user` scope to be consented.** If the SPA doesn't request it, the API can authenticate the user but can't call Graph on their behalf. First Graph call fails with a consent error, not a 401.
- **`EnableTokenAcquisitionToCallDownstreamApi` + `AddInMemoryTokenCaches` means token cache is per-instance.** Fine for development and single-instance deployments. For multi-instance, switch to `AddDistributedTokenCaches` backed by Redis — note this in your production checklist.
- **Claims transformation runs on every request.** The guard (`HasClaim("app_user_id")`) prevents re-lookup after the first run in a request, but the `FindByEntraOidAsync` lookup still runs per request. Consider caching the `oid → UserId` map in `HybridCache` with a short TTL if this shows up in profiling.
- **Deleting local auth is destructive.** Do it on a branch, run all arch tests, verify nothing in the template references password concepts before merging. Easy to miss a stray reference in a seeder or test fixture.

---

## Extensions

- **Organization-scoped authorization** (reporting chain, data scoping). Deliberately out of scope. If added, it becomes a separate Organization module with its own source-of-truth decision (Entra sync vs. app-owned). See `docs/howto/authorization/` (to be written).
- **Scheduled profile refresh.** A Wolverine scheduled job that refreshes all users' profiles weekly. Trade-off: keeps data fresh without user action, at the cost of sustained Graph load. Only add if the staleness of first-login data becomes a real complaint.
- **Conditional Access-aware error handling.** Entra may issue `claims challenge` responses requiring step-up auth (e.g., MFA for a sensitive action). Handling these cleanly means surfacing the challenge to the SPA. Defer until there's a concrete requirement.
- **Sign-out.** Entra sign-out is a redirect to Entra's logout endpoint; the API doesn't really participate beyond clearing any server-side session (which you don't have — tokens are bearer). Document in the SPA's auth flow, not here.

---

## Variant: Multi-tenant (B2B)

If the app is later used by multiple customer organizations:

- Change `AzureAd:TenantId` to `"common"` or `"organizations"`.
- Add `TokenValidationParameters.ValidateIssuer = false` and a custom `IssuerValidator` that accepts any tenant (or a known allowlist).
- `User` aggregate needs a `TenantId` alongside `EntraOid` — `oid` is only unique within a tenant. Unique index becomes `(TenantId, EntraOid)`.
- Every query that returns user data needs tenant filtering — likely via a global query filter. This is non-trivial and warrants its own how-to.
- App roles still work; they're defined per app registration, not per tenant.

Significant enough change that it's worth a separate how-to rather than trying to pre-wire it.

---

## Variant: Shape B — tenant-wide access (no assignment required)

Use this variant when the app is intended for *everyone in the tenant* — typical for internal tools like an employee directory, time tracker, or expense app where gating access per-user would be unnecessary admin overhead.

Changes from the default (Shape A):

1. **Enterprise app → Properties → Assignment required: No.** Every authenticated user in the tenant can now reach the app.
2. **Manifest:** keep only the `Admin` role. Remove the `User` role from `appRoles` — it's implicit.
3. **Tenant admin work:** only assign users to `Admin`. Everyone else just signs in; no explicit assignment needed.
4. **Claims transformation:** treat a missing `roles` claim as `User`. One-line addition in `UserProvisioningClaimsTransformation`:
   ```csharp
   var identity = (ClaimsIdentity)principal.Identity;
   if (!principal.HasClaim(c => c.Type == ClaimTypes.Role || c.Type == "roles"))
   {
       identity.AddClaim(new Claim(ClaimTypes.Role, "User"));
   }
   ```
5. **Fallback policy** stays the same — `RequireRole("Admin", "User")` still works because unassigned users now have `User` synthesized into their principal.

Trade-offs vs. Shape A:

- Lower admin burden (no per-user role assignment for standard users).
- Weaker access control by default — any tenant member can use the app. For a customer whose tenant contains only the people who should have access anyway, this is fine.
- The ADR should note which shape you're on; switching later is cheap but worth recording.

