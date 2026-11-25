namespace Sherlog.Core.Tests;

/// <summary>Finds files in the repository (for tests that use the real schema and library).</summary>
internal static class Repo
{
    public static string Root { get; } = FindRoot();

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    public static string SchemaPath => Path("library", "schema", "recipe.schema.json");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "Sherlog.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root (Sherlog.slnx).");
    }
}
