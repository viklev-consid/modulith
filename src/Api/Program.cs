using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Modulith.Api.Infrastructure.Exceptions;
using Modulith.Api.Infrastructure.FeatureFlags;
using Modulith.Modules.Catalog;
using Modulith.Modules.Users;
using Modulith.Shared.Infrastructure.Auth;
using Modulith.Shared.Infrastructure.Identity;
using Modulith.Shared.Infrastructure.Logging;
using Modulith.Shared.Infrastructure.Messaging;
using Modulith.Shared.Infrastructure.Seeding;
using Modulith.Shared.Infrastructure.Time;
using Modulith.Shared.Kernel.Interfaces;
using Scalar.AspNetCore;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// 1. Aspire service defaults (OTel, health checks, resilience, service discovery)
builder.AddServiceDefaults();

// 2. Serilog via appsettings.json with OTel sink
builder.Host.UseModulithSerilog();

// 3. ProblemDetails
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// 4. Authentication — JWT Bearer, symmetric signing key from user-secrets in dev
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOpts, jwtOpts) =>
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.Value.SigningKey));
        bearerOpts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = jwtOpts.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOpts.Value.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        bearerOpts.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized
                });
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden
                });
            }
        };
    });

// 5. Authorization with baseline policies
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
});

// 6. OpenAPI + Scalar
builder.Services.AddOpenApi();

// 7. Module registration
builder.Services
    .AddUsersModule(builder.Configuration, builder.Environment)
    .AddCatalogModule(builder.Configuration, builder.Environment);

// 8. API versioning
builder.Services.AddApiVersioning(opts =>
{
    opts.DefaultApiVersion = new ApiVersion(1, 0);
    opts.ReportApiVersions = true;
    opts.AssumeDefaultVersionWhenUnspecified = true;
    opts.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
});

// 9. Rate limiting with tiered policies (in-memory; for distributed limiting use ingress-layer)
builder.Services.AddRateLimiter(opts =>
{
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"global:{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 1000,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Sliding window, partitioned by IP — prevents credential-stuffing from authenticated tokens
    opts.AddPolicy("auth", ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(
            $"auth:{ip}",
            _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    opts.AddPolicy("write", ctx => PerUserPartition("write", ctx, 60));
    opts.AddPolicy("read", ctx => PerUserPartition("read", ctx, 300));
    opts.AddPolicy("expensive", ctx => PerUserPartition("expensive", ctx, 10));

    opts.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            ctx.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Title = "Too Many Requests",
                Status = StatusCodes.Status429TooManyRequests,
                Extensions =
                {
                    ["errorCode"] = "rate_limit_exceeded",
                    ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString()
                }
            },
            cancellationToken: token);
    };
});

// 10. HybridCache backed by Redis (gracefully degrades to L1-only without Redis)
builder.AddRedisDistributedCache("cache");
builder.Services.AddHybridCache();

// 11. Feature management with per-user targeting
builder.Services
    .AddFeatureManagement()
    .WithTargeting<CurrentUserTargetingContextAccessor>();

// 12. Wolverine — messaging, outbox, background jobs
builder.UseWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();

    opts.Policies.AddMiddleware<FluentValidationMiddleware>(_ => true);
    opts.Policies.AddMiddleware<AuditMiddleware>(_ => true);
    opts.Policies.AddMiddleware<CacheInvalidationMiddleware>(_ => true);

    // Register internal handlers per module (internal types require explicit inclusion)
    opts.AddUsersHandlers();
    opts.AddCatalogHandlers();
});

// Shared infrastructure services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();

// Health and liveness endpoints — exempt from rate limiting via Aspire ServiceDefaults
app.MapDefaultEndpoints();

// 13. Global exception handler (converts unhandled exceptions to ProblemDetails with traceId)
app.UseExceptionHandler();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().DisableRateLimiting();
    app.MapScalarApiReference().DisableRateLimiting();
}
else
{
    app.MapOpenApi().RequireAuthorization().DisableRateLimiting();
}

app.UseHttpsRedirection();

// 14. Module endpoint registrations
app.MapUsersEndpoints();
app.MapCatalogEndpoints();

// 15. Dev seeders (idempotent)
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeders = scope.ServiceProvider.GetServices<IModuleSeeder>();
    foreach (var seeder in seeders)
        await seeder.SeedAsync();
}

app.Run();

static RateLimitPartition<string> PerUserPartition(string policy, HttpContext ctx, int limit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        $"{policy}:{(ctx.User.Identity?.IsAuthenticated == true
            ? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon"
            : ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown")}",
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = limit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });

// Needed for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
