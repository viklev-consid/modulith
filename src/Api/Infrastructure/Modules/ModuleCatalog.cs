using System.Reflection;
using Modulith.Shared.Infrastructure.Modules;

namespace Modulith.Api.Infrastructure.Modules;

internal static class ModuleCatalog
{
    private const string moduleAssemblyPrefix = "Modulith.Modules.";
    private const string contractsAssemblySuffix = ".Contracts";

    public static IReadOnlyList<IModuleInstaller> DiscoverInstallers()
    {
        var installers = DiscoverModuleAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                     && t.IsAssignableTo(typeof(IModuleInstaller)))
            .Select(CreateInstaller)
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var duplicateNames = installers
            .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicateNames.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate module installer names found: {string.Join(", ", duplicateNames)}.");
        }

        return installers;
    }

    private static IEnumerable<Assembly> DiscoverModuleAssemblies()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(IsModuleAssembly);

        var baseDirectory = AppContext.BaseDirectory;
        var discovered = Directory.EnumerateFiles(baseDirectory, $"{moduleAssemblyPrefix}*.dll")
            .Select(AssemblyName.GetAssemblyName)
            .Where(IsModuleAssemblyName)
            .Select(LoadAssembly);

        return loaded
            .Concat(discovered)
            .DistinctBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase);
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

    private static bool IsModuleAssembly(Assembly assembly) =>
        IsModuleAssemblyName(assembly.GetName());

    private static bool IsModuleAssemblyName(AssemblyName assemblyName) =>
        assemblyName.Name is { } name
        && name.StartsWith(moduleAssemblyPrefix, StringComparison.Ordinal)
        && !name.EndsWith(contractsAssemblySuffix, StringComparison.Ordinal);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static IModuleInstaller CreateInstaller(Type type)
    {
        if (Activator.CreateInstance(type) is not IModuleInstaller installer)
        {
            throw new InvalidOperationException(
                $"Module installer '{type.FullName}' must have a public parameterless constructor.");
        }

        return installer;
    }
}
