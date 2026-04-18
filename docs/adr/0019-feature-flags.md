# ADR-0019: Microsoft.FeatureManagement with Startup/Runtime Split

## Status

Accepted

## Context

"Feature flag" covers two distinct needs:

1. **Static feature toggles** — "is this module enabled in this deployment?", "is this endpoint exposed in this environment?". Read once at startup, never changes without restart.
2. **Runtime feature flags** — genuinely flip at runtime without redeploying. Used for gradual rollout, A/B testing, kill switches for risky changes.

Conflating them produces confusion ("why didn't my flag take effect?" — because it's bound at startup) and encourages over-engineering.

Many teams reach for LaunchDarkly or Unleash early, then realize 90% of their flags are static. Others hard-code flags in config and regret it when they need dynamic rollout.

## Decision

Use **Microsoft.FeatureManagement** as the feature-flag library. Deliberately split by lifetime:

### Static (startup-bound) toggles → `IOptions<T>`

For module-level enable/disable, environment-specific feature sets, integration toggles:

```json
{
  "Modules": {
    "Orders": { "Enabled": true },
    "Analytics": { "Enabled": false }
  }
}
```

Read once during host composition. Affects which modules register, which middleware is added, which endpoints are exposed. Cannot change at runtime.

### Runtime flags → `IFeatureManager`

For rollout controls, kill switches, A/B experiments:

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
    }
  }
}
```

Evaluated per request. Supports filters (`PercentageFilter`, `TimeWindowFilter`, `ContextualTargetingFilter`) and external providers (Azure App Configuration, ConfigCat, LaunchDarkly) via plug-in.

### Key naming convention

| Prefix | Meaning | Lifetime |
|---|---|---|
| `Modules.<Name>.Enabled` | Module kill switch | Startup |
| `Features.<Module>.<Name>` | Feature branch inside a module | Runtime |
| `Experiments.<Name>` | A/B or percentage rollout | Runtime |

### Per-user targeting

A custom `ITargetingContextAccessor` pulls from the authenticated user:

```csharp
public sealed class CurrentUserTargetingContextAccessor : ITargetingContextAccessor
{
    private readonly ICurrentUser _user;
    public ValueTask<TargetingContext> GetContextAsync() =>
        new(new TargetingContext
        {
            UserId = _user.UserId?.ToString(),
            Groups = _user.Roles
        });
}
```

This enables per-user gradual rollout (`Rollout` filters) and group-based targeting (`Beta-Testers`).

### Archictural rules

- **No `IFeatureManager` in `Domain/`.** Domain does not know about flags. Enforced by architectural tests (ADR-0015).
- **Flags live at edges**: endpoint routing, handler selection, middleware. If a feature changes domain behavior, that's a domain change, not a flag.
- **No DB-backed admin UI.** Tempting, always a trap. Flag management UIs are a product; use an existing tool or stick to config.

## What NOT to use feature flags for

- Infrastructure switches (logging providers, cache backends). That's configuration.
- Long-lived feature branches. Merge or delete; feature flags are not a substitute for trunk-based development.
- Access control. That's authorization.
- Data migrations. That's a migration.

## Consequences

**Positive:**

- Clear separation between "this module is off" (restart required) and "this feature is off" (live toggle).
- Swapping config-driven flags for Azure App Configuration or ConfigCat is a provider-registration change, not a code change.
- Per-user targeting works out of the box via the custom accessor.
- No DB UI to build or maintain.

**Negative:**

- Developers must understand the split. An `IFeatureManager` flag doesn't control module registration, and an `IOptions` flag doesn't gradually roll out.
- `Microsoft.FeatureManagement` has a learning curve — filters, targeting context, custom feature definitions. Documented in `how-to/use-feature-flags.md`.
- Long-lived flags accumulate. Periodic flag cleanup is a team discipline, not a framework feature.

## Related

- ADR-0015 (Architectural Tests): enforces the no-`IFeatureManager`-in-Domain rule.
- ADR-0021 (Config and Secrets): Options-bound flags use the standard config hierarchy.
