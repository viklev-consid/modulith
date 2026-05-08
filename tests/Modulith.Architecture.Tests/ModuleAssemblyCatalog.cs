using System.Reflection;

namespace Modulith.Architecture.Tests;

internal static class ModuleAssemblyCatalog
{
    private const string moduleAssemblyPrefix = "Modulith.Modules.";
    private const string contractsAssemblySuffix = ".Contracts";

    public static IReadOnlyList<Assembly> ModuleAssemblies { get; } =
        DiscoverAssemblies(includeContracts: false);

    public static IReadOnlyList<Assembly> ContractsAssemblies { get; } =
        DiscoverAssemblies(includeContracts: true);

    private static Assembly[] DiscoverAssemblies(bool includeContracts)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => IsMatchingAssemblyName(a.GetName(), includeContracts));

        var discovered = Directory
            .EnumerateFiles(AppContext.BaseDirectory, $"{moduleAssemblyPrefix}*.dll")
            .Select(AssemblyName.GetAssemblyName)
            .Where(a => IsMatchingAssemblyName(a, includeContracts))
            .Select(LoadAssembly);

        return loaded
            .Concat(discovered)
            .DistinctBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Assembly LoadAssembly(AssemblyName assemblyName)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(
                a.GetName().Name,
                assemblyName.Name,
                StringComparison.OrdinalIgnoreCase));

        return loaded ?? Assembly.Load(assemblyName);
    }

    private static bool IsMatchingAssemblyName(AssemblyName assemblyName, bool includeContracts) =>
        assemblyName.Name is { } name
        && name.StartsWith(moduleAssemblyPrefix, StringComparison.Ordinal)
        && name.EndsWith(contractsAssemblySuffix, StringComparison.Ordinal) == includeContracts;
}
