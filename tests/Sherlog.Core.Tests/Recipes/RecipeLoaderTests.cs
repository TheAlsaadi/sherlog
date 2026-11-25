using Sherlog.Core.Diagnosis;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Tests.Recipes;

public class RecipeLoaderTests
{
    private static RecipeLoadResult Load(string folder) =>
        RecipeLoader.LoadFolders([Fixture.Path("recipes", folder)], Repo.SchemaPath);

    [Fact]
    public void Valid_recipe_loads_all_fields()
    {
        var result = Load("valid");

        Assert.Empty(result.Errors);
        var recipe = Assert.Single(result.Recipes);
        Assert.Equal("gpu-nvidia-driver-reset", recipe.Id);
        Assert.Equal(1, recipe.Version);
        Assert.Equal("NVIDIA graphics driver stopped responding", recipe.Title);
        Assert.Equal("driver-reset", recipe.Category);
        Assert.Single(recipe.References);
        Assert.Equal(100, recipe.Priority);

        Assert.Equal(2, recipe.Match.Count);
        Assert.Equal("display-reset", recipe.Match[0].Source);
        Assert.Equal("nvlddmkm", recipe.Match[0].FaultingModule);
        Assert.Equal(1, recipe.Match[0].ConstrainedFieldCount);
        Assert.Equal("0x00000116", recipe.Match[1].BugCheckCode);

        Assert.Equal(ConfidenceLevel.Likely, recipe.Confidence.Default);
        Assert.Equal([new RepeatRule(3, 7)], recipe.Confidence.ConfirmedWhen);

        Assert.StartsWith("Your NVIDIA graphics driver", recipe.Summary);
        Assert.NotNull(recipe.Details);

        Assert.Equal(["rollback-recent-display-driver", "guide-clean-driver-install"], recipe.Fixes.Select(f => f.Id));
        Assert.Equal("moderate", recipe.Fixes[0].Risk);
        Assert.Equal(["rollback_driver"], recipe.Fixes[0].Actions);

        var example = Assert.Single(recipe.Examples);
        Assert.Equal("nvlddmkm", example.FaultingModule);
        Assert.EndsWith("gpu-nvidia-driver-reset.yaml", recipe.FilePath);
    }

    [Theory]
    [InlineData("unknown-action")]
    [InlineData("confirmed-default")]
    [InlineData("missing-references")]
    public void Invalid_recipe_is_rejected_with_its_file_named(string name)
    {
        var result = RecipeLoader.LoadFolders([Fixture.Path("recipes", "invalid")], Repo.SchemaPath);

        Assert.Empty(result.Recipes);
        Assert.Contains(result.Errors, e => e.FilePath.EndsWith(name + ".yaml", StringComparison.Ordinal) && e.Message.Length > 0);
    }

    [Fact]
    public void Bad_file_does_not_stop_other_files()
    {
        var result = Load("mixed");

        Assert.Single(result.Recipes);
        var error = Assert.Single(result.Errors);
        Assert.EndsWith("bad.yaml", error.FilePath);
    }

    [Fact]
    public void Duplicate_id_is_an_error()
    {
        var result = Load("dupes");

        Assert.Single(result.Recipes);
        var error = Assert.Single(result.Errors);
        Assert.Contains("gpu-nvidia-driver-reset", error.Message);
    }

    [Fact]
    public void Yaml_syntax_error_is_reported_not_thrown()
    {
        var dir = Directory.CreateTempSubdirectory("sherlog-recipes-");
        try
        {
            File.WriteAllText(System.IO.Path.Combine(dir.FullName, "broken.yaml"), "id: [unclosed\n");

            var result = RecipeLoader.LoadFolders([dir.FullName], Repo.SchemaPath);

            Assert.Empty(result.Recipes);
            Assert.Single(result.Errors);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
