using System.Reflection;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Kernel.Domain;

namespace Modulith.Architecture.Tests;

/// <summary>
/// Enforces the vertical slice shape: slice types live in Features/, message types are sealed records,
/// and aggregates/entities only expose factory methods for construction.
/// See ADR-0002 (Vertical Slice Architecture) and ADR-0015 (Architectural Tests).
/// </summary>
[Trait("Category", "Architecture")]
public sealed class SliceConventionTests
{
    private static readonly Assembly[] AllModuleAssemblies =
    [
        typeof(User).Assembly,
        typeof(Product).Assembly,
        typeof(AuditEntry).Assembly,
        typeof(NotificationLog).Assembly,
    ];

    [Fact]
    public void FeatureHandlers_MustResideInFeaturesNamespace()
    {
        // Handler types that are feature-slice handlers must live under a *.Features.* namespace.
        // Integration subscribers (Integration/) and background job handlers (Jobs/) are excluded —
        // they are not feature slices and legitimately live elsewhere.
        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Where(t => t.Namespace?.Contains(".Integration.") != true)
            .Where(t => t.Namespace?.Contains(".Jobs") != true)
            .Where(t => t.Namespace?.Contains(".Features.") != true)
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Feature Handler types must reside in a '*.Features.*' namespace. " +
            "Place the handler under 'Features/<FeatureName>/' and name it '<Feature>Handler'. " +
            "Integration subscribers under 'Integration/' and job handlers under 'Jobs/' are exempt. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Validators_MustResideInFeaturesNamespace()
    {
        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Validator", StringComparison.Ordinal))
            .Where(t => t.Namespace?.Contains(".Features.") != true)
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Validator types must reside in a '*.Features.*' namespace. " +
            "Place the validator under 'Features/<FeatureName>/' and name it '<Feature>Validator'. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Endpoints_MustResideInFeaturesNamespace()
    {
        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Endpoint", StringComparison.Ordinal))
            .Where(t => t.Namespace?.Contains(".Features.") != true)
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Endpoint types must reside in a '*.Features.*' namespace. " +
            "Place the endpoint under 'Features/<FeatureName>/' and name it '<Feature>Endpoint'. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Commands_MustBeSealedRecords()
    {
        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(t => !t.IsSealed || !IsRecord(t))
            .Select(t => $"{t.FullName} (sealed={t.IsSealed}, record={IsRecord(t)})")
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Command types must be declared as 'sealed record'. " +
            "'sealed record' enforces immutability, value equality, and prevents subclassing. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Queries_MustBeSealedRecords()
    {
        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Query", StringComparison.Ordinal))
            .Where(t => !t.IsSealed || !IsRecord(t))
            .Select(t => $"{t.FullName} (sealed={t.IsSealed}, record={IsRecord(t)})")
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Query types must be declared as 'sealed record'. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Requests_MustBeSealedRecords()
    {
        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal))
            .Where(t => !t.IsSealed || !IsRecord(t))
            .Select(t => $"{t.FullName} (sealed={t.IsSealed}, record={IsRecord(t)})")
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Request types must be declared as 'sealed record'. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Responses_MustBeSealedRecords()
    {
        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Response", StringComparison.Ordinal))
            .Where(t => !t.IsSealed || !IsRecord(t))
            .Select(t => $"{t.FullName} (sealed={t.IsSealed}, record={IsRecord(t)})")
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Response types must be declared as 'sealed record'. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void AggregatesAndEntities_MustHaveNoPublicConstructors()
    {
        // Aggregates and entities must only be constructed via static factory methods (e.g. Create(...)).
        // Public constructors bypass invariant validation and the Result pattern.
        // EF Core hydration must use the private parameterless constructor.
        var aggregateRootBase = typeof(AggregateRoot<>);
        var entityBase = typeof(Entity<>);

        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => InheritsFromGenericBase(t, aggregateRootBase)
                     || InheritsFromGenericBase(t, entityBase))
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Aggregate and Entity types must not have public constructors. " +
            "Use a static factory method (e.g. 'public static Result<T> Create(...)') for construction. " +
            "The EF Core parameterless constructor must be private. " +
            "See ADR-0009 (Rich Domain Model). " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    // A record class has a compiler-generated '<Clone>$' method. Checking for its presence
    // is the reliable way to detect records via reflection without source-level attributes.
    private static bool IsRecord(Type t) => t.GetMethod("<Clone>$") is not null;

    private static bool InheritsFromGenericBase(Type type, Type genericBase)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericBase)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
