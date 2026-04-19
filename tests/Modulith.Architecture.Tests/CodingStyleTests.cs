namespace Modulith.Architecture.Tests;

public sealed class CodingStyleTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    public void AllSourceFiles_MustUseFileScopedNamespaces()
    {
        var repoRoot = FindRepoRoot();
        var violations = new List<string>();

        foreach (var directory in new[] { "src", "tests" })
        {
            var searchPath = Path.Combine(repoRoot, directory);
            if (!Directory.Exists(searchPath)) continue;

            foreach (var file in Directory.EnumerateFiles(searchPath, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGeneratedFile(file)) continue;

                if (ContainsBlockScopedNamespace(file))
                    violations.Add(Path.GetRelativePath(repoRoot, file));
            }
        }

        Assert.True(
            violations.Count == 0,
            "FAIL: The following files use block-scoped namespace declarations.\n" +
            "All C# files must use file-scoped syntax: 'namespace X.Y.Z;'\n\n" +
            "Violations:\n" +
            string.Join("\n", violations.Select(v => $"  - {v}")) + "\n\n" +
            "Fix: Change 'namespace X.Y.Z { ... }' to 'namespace X.Y.Z;' at the top of each file.");
    }

    private static bool ContainsBlockScopedNamespace(string filePath)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("namespace ", StringComparison.Ordinal) &&
                !trimmed.TrimEnd().EndsWith(';'))
                return true;
        }
        return false;
    }

    private static bool IsGeneratedFile(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
        path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Cannot locate the repository root. " +
            "Ensure 'Directory.Build.props' exists at the repository root.");
    }
}
