# Example: Security-Sensitive Slice (Enumeration Resistance)

**Pattern:** Public slice that must never reveal whether a user exists — always returns 200 regardless of outcome.

**Source:** `src/Modules/Users/Modulith.Modules.Users/Features/ForgotPassword/`

---

## The six files

### `ForgotPassword.Request.cs`

```csharp
namespace Modulith.Modules.Users.Features.ForgotPassword;

public sealed record ForgotPasswordRequest(string Email);
```

### `ForgotPassword.Response.cs`

```csharp
namespace Modulith.Modules.Users.Features.ForgotPassword;

public sealed record ForgotPasswordResponse;
```

Empty response body. The endpoint always returns 200 with this shape — there is nothing useful to return.

### `ForgotPassword.Command.cs`

```csharp
namespace Modulith.Modules.Users.Features.ForgotPassword;

internal sealed record ForgotPasswordCommand(string Email);
```

### `ForgotPassword.Validator.cs`

```csharp
using FluentValidation;

namespace Modulith.Modules.Users.Features.ForgotPassword;

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
```

### `ForgotPassword.Handler.cs`

```csharp
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Wolverine;

namespace Modulith.Modules.Users.Features.ForgotPassword;

public sealed class ForgotPasswordHandler(
    UsersDbContext db,
    ISingleUseTokenService tokenService,
    IOptions<UsersOptions> options,
    IMessageBus bus)
{
    public async Task<ErrorOr<ForgotPasswordResponse>> Handle(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        var emailResult = Email.Create(cmd.Email);

        // Invalid email format: still return success — don't reveal that the address is malformed
        if (emailResult.IsError)
            return new ForgotPasswordResponse();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailResult.Value, ct);

        if (user is not null)
        {
            // Issue a token and publish the event — Notifications module sends the email
            var (_, rawToken) = tokenService.Create(
                user.Id,
                TokenPurpose.PasswordReset,
                options.Value.PasswordResetTokenLifetime);

            await db.SaveChangesAsync(ct);

            await bus.PublishAsync(new PasswordResetRequestedV1(
                user.Id.Value,
                user.Email.Value,
                rawToken));              // raw token sent to Notifications only; never stored
        }

        // Identical response whether user exists or not
        return new ForgotPasswordResponse();
    }
}
```

### `ForgotPassword.Endpoint.cs`

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.ForgotPassword;

internal static class ForgotPasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.ForgotPassword,
            async (
                ForgotPasswordRequest request,
                [FromServices] IValidator<ForgotPasswordRequest> validator,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    // Return 200 even on validation failure — no useful information to an attacker
                    return Results.Ok(new ForgotPasswordResponse());
                }

                var command = new ForgotPasswordCommand(request.Email);
                var result = await bus.InvokeAsync<ErrorOr<ForgotPasswordResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ForgotPassword")
        .WithSummary("Request a password reset email. Always returns 200 regardless of whether the email exists.")
        .Produces<ForgotPasswordResponse>()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
```

---

## Key invariants

### Always 200

Every code path returns `200 OK` with the same `ForgotPasswordResponse` body. An attacker probing whether email addresses are registered cannot distinguish between the paths.

### Validation failure returns 200

The endpoint short-circuits validation failure as 200, not 422. If validation returned 422 and a valid email returned 200, an attacker could infer address validity by submitting addresses with and without the `@` sign.

### Same response body on every path

`ForgotPasswordResponse` has no fields. If you need to add a body (e.g., `{ "message": "..." }`), ensure the string is identical on every path — never vary the text based on whether the user was found.

### Raw token only flows to Notifications

`PasswordResetRequestedV1` carries `rawToken`. Only the Notifications module subscribes to this event, and it embeds the token in the email link. The raw token is never:
- Stored in the database (only its SHA-256 hash is stored)
- Logged by Serilog (Serilog destructuring masks known token property names, but don't rely on that)
- Returned in the HTTP response body

### Rate limit: `auth` policy

`POST /v1/users/password/forgot` uses the strictest rate-limit tier. Even though it always returns 200, repeated calls waste backend compute and could be used for targeted harassment (flooding someone's inbox). Rate limiting mitigates both.

---

## Testing

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task ForgotPassword_ReturnsSameResponseForKnownAndUnknownEmail()
{
    await fixture.SeedAsync(UserMother.WithEmail("known@example.com"));

    var r1 = await client.PostAsJsonAsync("/v1/users/password/forgot",
        new ForgotPasswordRequest("known@example.com"));
    var r2 = await client.PostAsJsonAsync("/v1/users/password/forgot",
        new ForgotPasswordRequest("unknown@example.com"));

    r1.StatusCode.ShouldBe(HttpStatusCode.OK);
    r2.StatusCode.ShouldBe(HttpStatusCode.OK);

    var body1 = await r1.Content.ReadAsStringAsync();
    var body2 = await r2.Content.ReadAsStringAsync();
    body1.ShouldBe(body2);
}

[Fact]
[Trait("Category", "Integration")]
public async Task ForgotPassword_TokenNotStoredInPlaintext()
{
    var user = await fixture.SeedAsync(UserMother.Active());
    await client.PostAsJsonAsync("/v1/users/password/forgot",
        new ForgotPasswordRequest(user.Email));

    var token = await fixture.QueryDb<UsersDbContext>(db =>
        db.SingleUseTokens.FirstOrDefaultAsync(t => t.UserId == user.Id.Value));

    token.ShouldNotBeNull();
    // Token hash stored as bytes, not the raw string
    token.TokenHash.Length.ShouldBe(32);  // SHA-256 = 32 bytes
}
```

---

## Related slices with the same invariant

`RequestEmailChange` (`POST /v1/users/me/email/request`) applies the same always-200 rule: even if the new email is already taken, the response is identical to a successful request. This prevents an attacker from enumerating taken email addresses via an authenticated account.

---

## Related

- [`../how-to/auth/implement-auth-flows.md`](../how-to/auth/implement-auth-flows.md)
- [`../adr/0028-auth-flows-baseline.md`](../adr/0028-auth-flows-baseline.md)
- [`../adr/0018-rate-limiting.md`](../adr/0018-rate-limiting.md)
