using Sherlog.Core.Bundles;
using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Tests.Diagnosis;

/// <summary>
/// The seeded-crash recipes (tests/seeded/recipes) load next to the real library, and each seeded crash resolves to its
/// own test recipe, so CI's seeded run checks the readers and matching rather than recipe overlap.
/// </summary>
public class SeededRecipesTests
{
    private static readonly RecipeLoadResult Loaded = RecipeLoader.LoadFolders(
        [Repo.Path("library", "recipes"), Repo.Path("tests", "seeded", "recipes")], Repo.SchemaPath);

    [Fact]
    public void Seeded_recipes_load_with_the_library()
    {
        Assert.Empty(Loaded.Errors.Select(e => $"{Path.GetFileName(e.FilePath)}: {e.Message}"));
        Assert.Equal(3, Loaded.Recipes.Count(r => r.Id.StartsWith("seed-", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("SherlogSeedCrash.exe", "SherlogSeedModule.dll", "0xc0000005", "seed-crash-in-module")]
    [InlineData("SherlogSeedOverflow.exe", "SherlogSeedOverflow.exe", "0xc00000fd", "seed-stack-overflow")]
    [InlineData("SherlogSeedFastFail.exe", "SherlogSeedFastFail.exe", "0xc0000409", "seed-fastfail")]
    public void Each_seeded_crash_resolves_to_its_test_recipe(string app, string module, string code, string recipe)
    {
        var crash = new CrashEvent("app-1", DateTimeOffset.UnixEpoch, EvidenceSources.ApplicationError, "Application Error", 1000,
            app, "0.0.0.0", module, "0.0.0.0", code, null, null, null);

        var diagnosis = new Diagnoser(Loaded.Recipes, TimeZoneInfo.Utc).Diagnose(CrashGrouper.Group([crash])[0]);

        Assert.Equal(recipe, diagnosis.Recipe?.Id);
    }

    [Fact]
    public void Expected_labels_name_every_seeded_program()
    {
        var label = LabelFile.Parse(File.ReadAllText(Repo.Path("tests", "seeded", "expected.yaml")));

        Assert.True(label.Investigator.Seeded);
        Assert.Equal(
            ["SherlogSeedCrash.exe", "SherlogSeedFastFail.exe", "SherlogSeedOverflow.exe"],
            label.Investigator.Labels.Select(l => l.AppName).Order(StringComparer.Ordinal));
        Assert.All(label.Investigator.Labels, l => Assert.Equal("seeded", l.LabelStrength));
    }
}
