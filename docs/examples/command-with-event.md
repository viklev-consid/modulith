# Example: Command Slice with Integration Event

**Pattern:** Write slice — validates input, creates an aggregate, publishes a public integration event.

**Source:** `src/Modules/Catalog/Modulith.Modules.Catalog/Features/CreateProduct/`

---

## The six files

### `CreateProduct.Request.cs`

```csharp
namespace Modulith.Modules.Catalog.Features.CreateProduct;

public sealed record CreateProductRequest(string Sku, string Name, decimal Price, string Currency);
```

Plain primitives. Domain types (`Sku`, `Money`) are internal — the wire boundary uses strings and decimals.

### `CreateProduct.Response.cs`

```csharp
namespace Modulith.Modules.Catalog.Features.CreateProduct;

public sealed record CreateProductResponse(Guid ProductId, string Sku, string Name, decimal Price, string Currency);
```

### `CreateProduct.Command.cs`

```csharp
namespace Modulith.Modules.Catalog.Features.CreateProduct;

public sealed record CreateProductCommand(string Sku, string Name, decimal Price, string Currency);
```

In this slice the command mirrors the request exactly. Typed IDs aren't needed here since the product doesn't exist yet. When the command updates an existing aggregate, use typed IDs: `new ProductId(id)` mapped from the route parameter in the endpoint.

### `CreateProduct.Validator.cs`

```csharp
using FluentValidation;

namespace Modulith.Modules.Catalog.Features.CreateProduct;

internal sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);
    }
}
```

Validates the `Request`, not the `Command`. Format and presence only — business invariants (e.g., SKU uniqueness) live in the handler and domain.

### `CreateProduct.Handler.cs`

```csharp
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Contracts.Events;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Errors;
using Modulith.Modules.Catalog.Persistence;
using Wolverine;

namespace Modulith.Modules.Catalog.Features.CreateProduct;

public sealed class CreateProductHandler(CatalogDbContext db, IMessageBus bus)
{
    public async Task<ErrorOr<CreateProductResponse>> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var skuResult = Sku.Create(cmd.Sku);                    // value object factory — validates format
        if (skuResult.IsError)
            return skuResult.Errors;

        var sku = skuResult.Value;

        if (await db.Products.AnyAsync(p => p.Sku == sku, ct)) // uniqueness check before aggregate creation
            return CatalogErrors.SkuAlreadyExists;

        var priceResult = Money.Create(cmd.Price, cmd.Currency);
        if (priceResult.IsError)
            return priceResult.Errors;

        var productResult = Product.Create(sku, cmd.Name, priceResult.Value); // aggregate factory
        if (productResult.IsError)
            return productResult.Errors;

        var product = productResult.Value;
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);                          // Wolverine AutoApplyTransactions wraps this

        await bus.PublishAsync(new ProductCreatedV1(            // outbox — published after commit
            product.Id.Value,
            product.Sku.Value,
            product.Name,
            product.Price.Amount,
            product.Price.Currency));

        return new CreateProductResponse(
            product.Id.Value,
            product.Sku.Value,
            product.Name,
            product.Price.Amount,
            product.Price.Currency);
    }
}
```

Key choices:
- Value object factories run first — fail fast on format errors before hitting the DB.
- Uniqueness check happens in the handler (not the domain) because it requires a DB query.
- `bus.PublishAsync` after `SaveChangesAsync` — the message goes into the Wolverine outbox in the same transaction. It is delivered reliably even if the process crashes after commit.
- Errors come from `CatalogErrors` — never inline strings.

### `CreateProduct.Endpoint.cs`

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Catalog.Features.CreateProduct;

internal static class CreateProductEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(CatalogRoutes.Products,
            async (
                CreateProductRequest request,
                [FromServices] IValidator<CreateProductRequest> validator,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary(),
                        statusCode: StatusCodes.Status422UnprocessableEntity);

                var command = new CreateProductCommand(request.Sku, request.Name, request.Price, request.Currency);
                var result = await bus.InvokeAsync<ErrorOr<CreateProductResponse>>(command, ct);
                return result.ToProblemDetailsOr(r => Results.Created($"{CatalogRoutes.Products}/{r.ProductId}", r));
            })
        .WithName("CreateProduct")
        .WithSummary("Create a new product.")
        .Produces<CreateProductResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization("Authenticated");
}
```

Key choices:
- Validator is injected and run explicitly in the endpoint — runs before the command is constructed.
- `Results.Created(location, body)` — 201 with a `Location` header pointing at the new resource.
- `.ProducesProblem(409)` is declared so Scalar documents the conflict case.
- `RequireAuthorization("Authenticated")` — catalog writes need a valid JWT.

---

## Checklist for a new command slice

- [ ] Validator on the `Request`, not the `Command`
- [ ] All error codes in `{Module}Errors.cs`, not inline
- [ ] `bus.PublishAsync(...)` after `SaveChangesAsync` (outbox delivery)
- [ ] `ToProblemDetailsOr(...)` in the endpoint, not manual `if (result.IsError)` branching
- [ ] OpenAPI metadata declared (`.Produces`, `.ProducesProblem`)
- [ ] Endpoint registered in `CatalogModule.MapCatalogEndpoints`
- [ ] Integration test for happy path + conflict case

---

## Related

- [`../how-to/add-a-slice.md`](../how-to/add-a-slice.md)
- [`../how-to/handle-failures.md`](../how-to/handle-failures.md)
- [`../how-to/cross-module-events.md`](../how-to/cross-module-events.md)
- [`../adr/0003-wolverine-for-messaging.md`](../adr/0003-wolverine-for-messaging.md)
- [`../adr/0004-result-pattern.md`](../adr/0004-result-pattern.md)
