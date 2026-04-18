# ADR-0024: Scalar for OpenAPI Documentation

## Status

Accepted

## Context

For interactive API documentation, the choices are:

1. **Swashbuckle (Swagger UI)** — the long-standing default. Still widely used but increasingly dated. Its maintenance model and relationship to the underlying tooling has been fraught.
2. **NSwag** — alternative generator and UI. Fine but no particular advantage now.
3. **Scalar** — modern OpenAPI UI. Clean, fast, actively developed, pairs cleanly with .NET 10's built-in `Microsoft.AspNetCore.OpenApi`.

.NET 10 ships first-class OpenAPI document generation (`Microsoft.AspNetCore.OpenApi`). The generation is separate from the UI. That means we get to pick a UI based on UI merits, not library coupling.

Scalar wins on UX (better navigation, better schema explorer, better code samples), on dev experience (simpler integration), and on momentum (actively maintained). Swashbuckle brings nothing distinctive at this point.

## Decision

Use `Microsoft.AspNetCore.OpenApi` to generate the OpenAPI document (part of .NET 10). Use **Scalar** (`Scalar.AspNetCore` NuGet package) to serve the interactive UI.

### Wiring

```csharp
// Program.cs
builder.Services.AddOpenApi();

app.MapOpenApi();           // /openapi/v1.json
app.MapScalarApiReference(); // /scalar/v1 (dev only)
```

### API versioning integration

With `Asp.Versioning.Http`, multiple OpenAPI documents are generated (one per version). Scalar supports this natively.

### Enrichment

OpenAPI metadata on endpoints:

- `.WithName(...)` — operationId
- `.WithSummary(...)` / `.WithDescription(...)`
- `.Produces<T>(200)` — response types
- `.ProducesProblem(400)` / `.ProducesValidationProblem(422)` — error responses

These are applied in the endpoint files, alongside the route mapping.

### Availability

Scalar UI is **dev-only by default**. Production OpenAPI doc is available at `/openapi/v1.json` (behind the same auth the rest of the API uses) but the UI is not exposed unless explicitly enabled.

### Do NOT use Swashbuckle in this template

If a team needs Swashbuckle for internal reasons (custom filters, existing tooling), they can swap. But no Swashbuckle dependencies ship.

## Consequences

**Positive:**

- Better UX than Swagger UI.
- First-class support for multiple API versions.
- No dependency on Swashbuckle's release cadence or maintenance model.
- OpenAPI generation is framework-level (Microsoft), UI is modular — replacing the UI doesn't affect documentation accuracy.

**Negative:**

- Scalar is newer than Swashbuckle. Less Stack Overflow coverage. Mitigated by good documentation from the Scalar team.
- Teams comfortable with Swagger UI have a small adjustment. Feature parity is there; cosmetics differ.

## Related

- ADR-0022 (Testing): snapshot tests (Verify) capture the OpenAPI document shape for contract regression testing.
