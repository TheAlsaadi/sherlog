namespace Sherlog.Cli.Tests;

public class RecipesCommandTests
{
    [Fact]
    public void Recipes_check_passes_on_valid_library()
    {
        var cli = new CliHarness();

        Assert.Equal(0, cli.Run("recipes", "check", "--library", CliHarness.ValidRecipes));
        Assert.Contains("1 recipe OK", cli.Out.ToString());
    }

    [Fact]
    public void Recipes_check_fails_on_invalid_library()
    {
        var cli = new CliHarness();

        Assert.Equal(1, cli.Run("recipes", "check", "--library", CliHarness.MixedRecipes));
        Assert.Contains("bad.yaml", cli.Error.ToString());
    }

    [Fact]
    public void Recipes_check_uses_the_bundled_library_by_default()
    {
        var cli = new CliHarness();

        Assert.Equal(0, cli.Run("recipes", "check"));
        Assert.Contains("OK", cli.Out.ToString());
    }

    [Fact]
    public void Missing_library_folder_is_an_error()
    {
        var cli = new CliHarness();

        Assert.Equal(1, cli.Run("recipes", "check", "--library", "Z:\\no\\such\\folder"));
        Assert.Contains("no such folder", cli.Error.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
