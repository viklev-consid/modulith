using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Modulith.Modules.Users;
using Modulith.Shared.Infrastructure.Seeding;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Authentication
var jwtIssuer = builder.Configuration["Modules:Users:JwtIssuer"];
var jwtAudience = builder.Configuration["Modules:Users:JwtAudience"];
var jwtKey = builder.Configuration["Modules:Users:JwtKey"]
    ?? throw new InvalidOperationException("Modules:Users:JwtKey is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// Module registration
builder.Services.AddUsersModule(builder.Configuration, builder.Environment);

// Wolverine — discovers handlers in all registered module assemblies
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(UsersModule).Assembly);
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Module endpoints
app.MapUsersEndpoints();

// Dev seeders
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeders = scope.ServiceProvider.GetServices<IModuleSeeder>();
    foreach (var seeder in seeders)
        await seeder.SeedAsync();
}

app.Run();

// Needed for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
