using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;

namespace Sherlog.Cli.Commands;

/// <summary>The shared pipeline: load recipes, read crashes, group them and diagnose each group.</summary>
internal static class Analysis
{
    public static RecipeLoadResult LoadRecipes(CliEnvironment env, string[]? libraryFolders)
    {
        var folders = libraryFolders is { Length: > 0 } ? libraryFolders : [Path.Combine(env.LibraryRoot, "recipes")];
        foreach (var folder in folders.Where(f => !Directory.Exists(f)))
        {
            throw new UserErrorException($"There's no such folder: {folder}");
        }

        return RecipeLoader.LoadFolders(folders, Path.Combine(env.LibraryRoot, "schema", "recipe.schema.json"));
    }

    public static IReadOnlyList<Diagnosis> Run(CliEnvironment env, string? bundle, string[]? libraryFolders, int days)
    {
        if (days < 1)
        {
            throw new UserErrorException("--days must be 1 or more.");
        }

        var recipes = LoadRecipes(env, libraryFolders);
        foreach (var error in recipes.Errors)
        {
            env.Error.WriteLine($"Skipped recipe {Path.GetFileName(error.FilePath)}: {error.Message}");
        }

        IRecordSource source;
        try
        {
            source = env.OpenSource(bundle);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or InvalidDataException or IOException)
        {
            throw new UserErrorException($"Couldn't open the evidence bundle {bundle}: {ex.Message}");
        }

        // A bundle's "last N days" count back from when it was made, not from today.
        var now = source is Sherlog.Core.Bundles.BundleRecordSource bundled ? bundled.CreatedUtc : env.Clock.GetUtcNow();
        var reader = new EvidenceReader(source);
        var crashes = reader.ReadCrashes(now.AddDays(-days));
        foreach (var warning in reader.Warnings.Distinct())
        {
            env.Error.WriteLine(warning);
        }

        return new Diagnoser(recipes.Recipes, env.TimeZone).DiagnoseAll(CrashGrouper.Group(crashes));
    }
}
