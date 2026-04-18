# ADR-0017: HybridCache for Data Caching

## Status

Accepted

## Context

.NET 10 ships **HybridCache** — a two-tier cache (L1 in-memory + L2 distributed) with request coalescing, tag-based invalidation, and uniform API. It replaces the older pattern of combining `IMemoryCache` with `IDistributedCache` manually.

Key problems HybridCache solves:

- **Cache stampede** — multiple concurrent requests for the same missing key all hit the backend. HybridCache coalesces them into one.
- **L1/L2 dance** — with raw IDistributedCache, you either skip the in-memory cache (every read crosses the wire) or maintain both manually (bug-prone). HybridCache handles both.
- **Tag-based invalidation** — `RemoveByTagAsync` for "invalidate everything related to user X" without tracking keys manually.
- **Uniform API** — one abstraction instead of two.

Custom caching on top of raw `IMemoryCache` + `IDistributedCache` in a new .NET 10 codebase would be a regression.

## Decision

Use HybridCache as the data-caching primitive.

### Wiring

- Redis provisioned by Aspire (`builder.AddRedis("cache")` in AppHost).
- API: `builder.AddRedisDistributedCache("cache")` then `builder.Services.AddHybridCache()`.
- Without Redis, HybridCache falls back to L1-only. This means local dev without Redis still works.

### Key convention

Keys follow `{module}:{entity}:{id}[:variant]`:

- `users:user:abc-123`
- `users:user:abc-123:profile`
- `catalog:product:sku-42`

Modules own their own keyspace. A module must never invalidate another module's keys directly. Cross-module invalidation happens via integration events — the owning module listens and invalidates its own keys.

A `CacheKey` helper in `Shared.Infrastructure` constructs keys consistently:

```csharp
CacheKey.For<User>(userId)           // "users:user:<id>"
CacheKey.For<User>(userId, "profile") // "users:user:<id>:profile"
```

Module prefix is derived from the type's namespace. Keys are never hand-concatenated in feature code.

### Invalidation

Two mechanisms:

1. **Attribute-driven**: commands decorated with `[InvalidatesCache("users:user:{UserId}")]` are intercepted by a Wolverine middleware that parses the template and calls `RemoveAsync` post-commit.
2. **Tag-based**: for sweeping invalidations, use tags: `await cache.RemoveByTagAsync("user:" + userId)`.

Invalidation happens **post-commit**. A command that fails must not invalidate; the middleware observes the command result before invalidating.

### Cross-cutting: GDPR erasure

`IPersonalDataEraser` implementations must flush cached data for the user. The base class `PersonalDataEraserBase` handles common cache flushes for the module; custom entries are responsibility of the derived eraser.

### Serialization

Default JSON. MessagePack is faster but adds a dependency; teams that need the perf gain can swap. Not the template's job to optimize this.

### Output caching vs. data caching

HTTP-level caching (ETag, Cache-Control, output caching middleware) is a separate concern. Use ASP.NET Core's output caching middleware for that. Do not conflate the two.

## What NOT to cache

- Anything user-specific without tenant/user in the key.
- Anything mutated by events from other modules unless the subscribing module also invalidates.
- Personal data that could leak across erasure boundaries (unless the eraser correctly flushes).
- Large blobs (use the blob store).

## Consequences

**Positive:**

- Stampede-safe by default.
- Graceful degradation without Redis — local dev doesn't require it.
- Uniform caching API.
- Invalidation is declarative via attributes for common cases.
- Keys are typed via the helper, reducing typo bugs.

**Negative:**

- HybridCache is new in .NET 10. Examples and Stack Overflow coverage are still growing.
- Attribute-driven invalidation requires a template-string parser and command-property reflection. Implemented once in the shared middleware, but has edge cases (missing properties, nullable keys).
- Cache invalidation across modules depends on event reliability. The outbox makes this correct, but eventually consistent — reads right after a cross-module write may hit stale cache briefly.

## Related

- ADR-0003 (Wolverine): the middleware pipeline where invalidation lives.
- ADR-0005 (Module Communication): cross-module cache invalidation via events.
- ADR-0012 (GDPR): cache flush as part of erasure.
