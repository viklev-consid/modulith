# Examples

Worked patterns extracted from the real modules. Each file shows one pattern end-to-end with annotations pointing at the key decisions.

| File | Pattern | Source module |
|---|---|---|
| [`simple-query-slice.md`](simple-query-slice.md) | Read-only slice with no validator | Catalog / `ListProducts` |
| [`command-with-event.md`](command-with-event.md) | Command slice that publishes an integration event | Catalog / `CreateProduct` |
| [`cross-module-subscriber.md`](cross-module-subscriber.md) | Integration event subscriber with idempotency and consent | Notifications / `OnUserRegisteredHandler` |
| [`scheduled-job.md`](scheduled-job.md) | Self-rescheduling Wolverine background job | Users / `SweepExpiredTokens` |
| [`security-sensitive-slice.md`](security-sensitive-slice.md) | Slice with enumeration resistance (always-200) | Users / `ForgotPassword` |

These are the real files, not hypotheticals. If a file path below drifts from the actual codebase, trust the source — the paths are relative to the repo root.
