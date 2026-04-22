# CLAUDE.md — Catalog Module

This module owns the product catalog: creation, pricing, and availability. It is a simple reference module — its primary purpose is proving that a second module can exist alongside Users without boundary violations.

---

## Domain vocabulary

- **Product** — the root aggregate. Identified by `ProductId` (typed Guid).
- **Sku** — a value object. Always stored as uppercase, trimmed. Maximum 50 characters.
- **Money** — a value object with `Amount` (decimal) and `Currency` (3-letter ISO 4217 code). Amount cannot be negative.
- **IsActive** — whether the product is available for sale. Deactivation is one-way via `Deactivate()`.

---

## Invariants

1. SKU addresses are unique across the `catalog` schema.
2. `Product.Create(...)` raises `ProductCreated` domain event. The handler publishes `ProductCreatedV1` after the database write.
3. Price is stored as `(amount, currency)` — a `Money` owned entity. No currency conversion happens in this module.
4. `Deactivate()` is irreversible — once a product is inactive, it cannot be reactivated (no re-activation method exists by design; raise a new issue if needed).

---

## Access control

- `ListProducts` and `GetProductById` are public (no auth required).
- `CreateProduct` requires `CatalogPermissions.ProductsWrite` (`catalog.products.write`), which is granted to the `admin` role.

Permission constants live in `Modulith.Modules.Catalog.Contracts/Authorization/CatalogPermissions.cs`.

---

## Schema

- `catalog` Postgres schema. One migration history table.
- `Price` is stored as an EF owned entity with columns `price_amount` and `price_currency`.

---

## Known footguns

- `Sku.Create(...)` returns `ErrorOr<Sku>`. Do not use `new Sku(...)` directly — the constructor is private.
- `Money.Create(...)` similarly. The EF value converter in `ProductConfiguration` calls `Sku.Create(...).Value` — this is safe only because EF reads well-formed data written by this module.
- Cross-module references: use only `Catalog.Contracts` events. Do not reference `Modulith.Modules.Catalog` internals from other modules.
