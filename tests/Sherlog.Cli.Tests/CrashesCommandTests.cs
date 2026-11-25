using System.Text.Json;

namespace Sherlog.Cli.Tests;

public class CrashesCommandTests
{
    [Fact]
    public void Crashes_lists_groups_newest_first_with_confidence()
    {
        var cli = new CliHarness();
        cli.AddNvidiaResets(3);
        cli.Source.AddAppError(1, CliHarness.Now.AddDays(-5));

        var exit = cli.Run("crashes", "--library", CliHarness.ValidRecipes);

        Assert.Equal(0, exit);
        var text = cli.Out.ToString();
        var nvidia = text.IndexOf("Your NVIDIA graphics driver stopped responding", StringComparison.Ordinal);
        var game = text.IndexOf("SherlogTestGame.exe", StringComparison.Ordinal);
        Assert.True(nvidia >= 0 && game > nvidia, text);
        Assert.Contains("Confirmed", text);
        Assert.Contains("Unclear", text);
        Assert.Contains("3 times", text);
        Assert.Contains("g-", text);
        Assert.Contains("sherlog explain", text);
    }

    [Fact]
    public void Crashes_with_no_crashes_says_so_and_exits_0()
    {
        var cli = new CliHarness();

        var exit = cli.Run("crashes", "--library", CliHarness.ValidRecipes);

        Assert.Equal(0, exit);
        Assert.Equal("No crashes found in the last 30 days.", cli.Out.ToString().Trim());
    }

    [Fact]
    public void Days_option_limits_the_window()
    {
        var cli = new CliHarness();
        cli.Source.AddAppError(1, CliHarness.Now.AddDays(-10));

        cli.Run("crashes", "--days", "7", "--library", CliHarness.ValidRecipes);

        Assert.Equal("No crashes found in the last 7 days.", cli.Out.ToString().Trim());
    }

    [Fact]
    public void Crashes_json_is_valid_and_has_group_ids()
    {
        var cli = new CliHarness();
        cli.AddNvidiaResets(1);

        var exit = cli.Run("crashes", "--json", "--library", CliHarness.ValidRecipes);

        Assert.Equal(0, exit);
        using var doc = JsonDocument.Parse(cli.Out.ToString());
        var group = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.StartsWith("g-", group.GetProperty("id").GetString());
        Assert.Equal("Likely", group.GetProperty("confidence").GetString());
        Assert.Equal("gpu-nvidia-driver-reset", group.GetProperty("recipe").GetString());
        Assert.Equal(1, group.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Bad_recipe_warns_but_diagnosis_continues()
    {
        var cli = new CliHarness();
        cli.AddNvidiaResets(1);

        var exit = cli.Run("crashes", "--library", CliHarness.MixedRecipes);

        Assert.Equal(0, exit);
        Assert.Contains("bad.yaml", cli.Error.ToString());
        Assert.Contains("Your NVIDIA graphics driver stopped responding", cli.Out.ToString());
    }

    [Fact]
    public void Source_warning_is_printed_once()
    {
        var cli = new CliHarness();
        cli.Source.WarningList.Add("Couldn't read the System log.");

        cli.Run("crashes", "--library", CliHarness.ValidRecipes);

        var error = cli.Error.ToString();
        Assert.Equal(error.IndexOf("Couldn't read the System log.", StringComparison.Ordinal),
            error.LastIndexOf("Couldn't read the System log.", StringComparison.Ordinal));
        Assert.Contains("Couldn't read the System log.", error);
    }

    [Fact]
    public void Unknown_bundle_path_exits_1_with_plain_message()
    {
        var cli = new CliHarness();

        var exit = cli.Run("crashes", "--bundle", "missing.zip", "--library", CliHarness.ValidRecipes);

        Assert.Equal(1, exit);
        Assert.Contains("missing.zip", cli.Error.ToString());
    }
}
