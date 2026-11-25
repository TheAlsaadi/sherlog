using System.Text.Json;

namespace Sherlog.Cli.Tests;

public class ExplainCommandTests
{
    [Fact]
    public void Explain_latest_shows_summary_confidence_fixes_and_no_changes_line()
    {
        var cli = new CliHarness();
        cli.AddNvidiaResets(1);

        var exit = cli.Run("explain", "--latest", "--library", CliHarness.ValidRecipes);

        Assert.Equal(0, exit);
        var text = cli.Out.ToString();
        Assert.Contains("Likely: Your NVIDIA graphics driver stopped responding", text);
        Assert.Contains("What Windows recorded", text);
        Assert.Contains("nvlddmkm", text);
        Assert.Contains("1. ", text);
        Assert.Contains("2. ", text);
        Assert.Contains("Sherlog doesn't change anything on your PC in this version.", text);
    }

    [Fact]
    public void Explain_by_group_id_and_by_crash_id_give_the_same_group()
    {
        var cli = new CliHarness();
        cli.Source.AddAppError(4242, CliHarness.Now.AddDays(-1));
        cli.Run("crashes", "--json", "--library", CliHarness.ValidRecipes);
        var groupId = JsonDocument.Parse(cli.Out.ToString()).RootElement[0].GetProperty("id").GetString()!;

        var byGroup = new CliHarness();
        byGroup.Source.AddAppError(4242, CliHarness.Now.AddDays(-1));
        Assert.Equal(0, byGroup.Run("explain", groupId, "--library", CliHarness.ValidRecipes));

        var byCrash = new CliHarness();
        byCrash.Source.AddAppError(4242, CliHarness.Now.AddDays(-1));
        Assert.Equal(0, byCrash.Run("explain", "app-4242", "--library", CliHarness.ValidRecipes));

        Assert.Equal(byGroup.Out.ToString(), byCrash.Out.ToString());
        Assert.Contains("Unclear", byGroup.Out.ToString());
    }

    [Fact]
    public void Explain_unknown_id_exits_1_with_plain_message()
    {
        var cli = new CliHarness();
        cli.AddNvidiaResets(1);

        var exit = cli.Run("explain", "g-000000", "--library", CliHarness.ValidRecipes);

        Assert.Equal(1, exit);
        Assert.Contains("g-000000", cli.Error.ToString());
    }

    [Fact]
    public void Explain_without_id_or_latest_exits_1()
    {
        var cli = new CliHarness();

        Assert.Equal(1, cli.Run("explain", "--library", CliHarness.ValidRecipes));
        Assert.Contains("--latest", cli.Error.ToString());
    }

    [Fact]
    public void Explain_latest_with_no_crashes_says_so_and_exits_0()
    {
        var cli = new CliHarness();

        Assert.Equal(0, cli.Run("explain", "--latest", "--library", CliHarness.ValidRecipes));
        Assert.Contains("No crashes found", cli.Out.ToString());
    }

    [Fact]
    public void Explain_json_has_fixes_and_what_we_know()
    {
        var cli = new CliHarness();
        cli.AddNvidiaResets(1);

        cli.Run("explain", "--latest", "--json", "--library", CliHarness.ValidRecipes);

        var root = JsonDocument.Parse(cli.Out.ToString()).RootElement;
        Assert.Equal("gpu-nvidia-driver-reset", root.GetProperty("recipe").GetString());
        Assert.Equal(2, root.GetProperty("fixes").GetArrayLength());
        Assert.Contains("nvlddmkm", root.GetProperty("whatWeKnow").GetString());
    }
}
