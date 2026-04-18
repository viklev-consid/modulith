# How-to: Use Feature Flags

Modulith uses `Microsoft.FeatureManagement` with a deliberate split between startup-bound flags and runtime flags. This guide walks through when to use which.

For reasoning, see [`../adr/0019-feature-flags.md`](../adr/0019-feature-flags.md).

---

## The two lifetimes

| Need | Lifetime | Mechanism |
|---|---|---|
| Is this module enabled in this deployment? | Startup | `IOptions<T>` from config |
| Is this feature branch live for all users? | Startup | `IOptions<T>` from config |
| Is this feature enabled for user X specifically? | Runtime | `IFeatureManager` |
| Gradually roll out to 20% of users? | Runtime | `IFeatureManager` + `PercentageFilter` |
| Kill-switch a risky change without redeploying? | Runtime | `IFeatureManager` + external provider |

Startup flags are config. Runtime flags are evaluated per request.

---

## Key naming convention

| Prefix | Example | Use |
|---|---|---|
| `Modules.<n>.Enabled` | `Modules.Analytics.Enabled` | Module kill switch (startup) |
| `Features.<Module>.<n>` | `Features.Orders.NewPricingEngine` | Feature branch (runtime) |
| `Experiments.<n>` | `Experiments.CheckoutFlowV2` | A/B or percentage rollout |

Arch test enforces naming patterns.

---

## Startup flags (IOptions)

For module enablement, major feature branches, and anything that binds at startup:

### In config

```json
{
  "Modules": {
    "Orders": { "Enabled": true },
    "Analytics": { "Enabled": false }
  }
}
```

### In code

Define `ModuleSwitchOptions`:

```csharp
public sealed class ModuleSwitchOptions
{
    public bool Enabled { get; init; } = true;
}
```

Register in the module's registration:

```csharp
public static IServiceCollection AddAnalyticsModule(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var moduleConfig = configuration.GetSection("Modules:Analytics");
    var enabled = moduleConfig.GetValue("Enabled", defaultValue: true);

    if (!enabled)
        return services;   // skip registration entirely

    // ... normal registration
    return services;
}
```

Changing this flag requires a restart.

---

## Runtime flags (IFeatureManager)

For per-user or percentage-based gating:

### In config

```json
{
  "FeatureManagement": {
    "Features.Orders.NewPricingEngine": true,
    "Experiments.CheckoutFlowV2": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": { "Value": 20 }
        }
      ]
    },
    "Features.Users.BetaFeatures": {
      "EnabledFor": [
        {
          "Name": "Targeting",
          "Parameters": {
            "Audience": {
              "Groups": [
                { "Name": "beta-testers", "RolloutPercentage": 100 }
              ]
            }
          }
        }
      ]
    }
  }
}
```

### In code

Inject `IFeatureManager` and evaluate:

```csharp
public sealed class PlaceOrderHandler
{
    private readonly IFeatureManager _features;
    private readonly IPricingEngine _legacy;
    private readonly INewPricingEngine _new;

    public PlaceOrderHandler(IFeatureManager features, IPricingEngine legacy, INewPricingEngine @new)
    {
        _features = features;
        _legacy = legacy;
        _new = @new;
    }

    public async Task<ErrorOr<Response>> Handle(Command cmd, CancellationToken ct)
    {
        var useNew = await _features.IsEnabledAsync("Features.Orders.NewPricingEngine");
        var engine = useNew ? _new : _legacy;
        var price = await engine.CalculateAsync(cmd.Items, ct);
        // ...
    }
}
```

**Arch test forbids `IFeatureManager` in `Domain/` folders.** Feature flags live at the edges: handlers, endpoint routing, middleware.

---

## Per-user targeting

`ITargetingContextAccessor` pulls from the authenticated user:

```csharp
public sealed class CurrentUserTargetingContextAccessor : ITargetingContextAccessor
{
    private readonly ICurrentUser _user;

    public CurrentUserTargetingContextAccessor(ICurrentUser user) => _user = user;

    public ValueTask<TargetingContext> GetContextAsync() =>
        ValueTask.FromResult(new TargetingContext
        {
            UserId = _user.UserId?.ToString(),
            Groups = _user.Roles.ToArray()
        });
}
```

Wired in `Program.cs`:

```csharp
builder.Services.AddFeatureManagement()
    .WithTargeting<CurrentUserTargetingContextAccessor>();
```

Then percentage rollouts and group targeting work per-user.

---

## Endpoint-level gating

Use the `[FeatureGate]` attribute on endpoint extensions:

```csharp
app.MapGet("/v1/beta-feature", ...)
    .WithMetadata(new FeatureGateAttribute("Features.Users.BetaFeatures"));
```

When disabled, the endpoint returns 404 (configurable to 403 or 503 if needed).

For minimal APIs specifically, a shared helper:

```csharp
public static RouteHandlerBuilder RequireFeature(this RouteHandlerBuilder builder, string feature) =>
    builder.AddEndpointFilter(async (ctx, next) =>
    {
        var mgr = ctx.HttpContext.RequestServices.GetRequiredService<IFeatureManager>();
        return await mgr.IsEnabledAsync(feature)
            ? await next(ctx)
            : Results.NotFound();
    });
```

Usage:

```csharp
app.MapGet("/v1/beta", ...).RequireFeature("Features.Users.BetaFeatures");
```

---

## External providers

The template ships with config-only flags. To switch to Azure App Configuration, ConfigCat, or LaunchDarkly, add the provider package and register it:

```csharp
// Example: Azure App Configuration
builder.Configuration.AddAzureAppConfiguration(opts =>
{
    opts.Connect(builder.Configuration["AppConfig:Endpoint"])
        .UseFeatureFlags();
});
```

`IFeatureManager` consumers don't change — they still call `IsEnabledAsync`. The provider is the difference.

---

## What NOT to flag

- **Infrastructure choices** (which cache, which email provider). That's config.
- **Access control** (can user X do Y). That's authorization.
- **Data migrations.** That's a migration.
- **Long-lived feature branches.** If a flag is still around after six months, delete the old code path or commit to keeping both.

---

## Testing

### Force flags in tests

```csharp
public sealed class OrdersApiFixture : ApiTestFixture
{
    protected override void ConfigureFeatureManagement(Dictionary<string, bool> flags)
    {
        flags["Features.Orders.NewPricingEngine"] = true;
    }
}
```

Or per-test:

```csharp
[Fact]
public async Task WithNewPricingEnabled_AppliesNewEngine()
{
    fixture.SetFeature("Features.Orders.NewPricingEngine", true);
    // ... act and assert
}
```

### Test both paths

Any flagged code path is really two code paths. Test both. A flag-on test and a flag-off test.

---

## Retiring a flag

When a feature is proven and should be the permanent behavior:

1. Remove the flag check from the handler.
2. Delete the old code path.
3. Remove the flag from config.
4. Update the integration test to remove the flag setup.
5. Note the removal in the changelog.

Flags are debt. Pay it down.

---

## Common mistakes

- **Using `IFeatureManager` in domain code.** Arch test will catch it.
- **Using `IOptions<T>` for runtime toggles.** Value is bound at startup — changing config has no effect until restart.
- **Flags for access control.** Roles/permissions are authorization. Flags are feature availability.
- **Flagging framework concerns.** Logging provider, cache backend, etc. — config, not flags.
- **Flag names without the convention prefix.** `NewPricing` won't pass arch test. Use `Features.Orders.NewPricingEngine`.
- **Long-lived flags that never get cleaned up.** Schedule a periodic flag audit.

---

## Related

- [`../adr/0019-feature-flags.md`](../adr/0019-feature-flags.md)
- [`../adr/0021-config-and-secrets.md`](../adr/0021-config-and-secrets.md)
