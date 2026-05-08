namespace Modulith.Modules.ModuleName.Contracts.Authorization;

public static class ModuleNamePermissions
{
    public const string Read = "modulenamelower.read";
    public const string Write = "modulenamelower.write";

    public static IReadOnlyCollection<string> All { get; } =
        [Read, Write];
}
