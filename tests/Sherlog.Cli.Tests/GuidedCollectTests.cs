using Sherlog.Core.Bundles;

namespace Sherlog.Cli.Tests;

/// <summary><c>sherlog</c> with no command (a friend double-clicking sherlog.exe) runs a guided collect.</summary>
public sealed class GuidedCollectTests : IDisposable
{
    private readonly string _desktop = Directory.CreateTempSubdirectory("sherlog-desktop-").FullName;

    public void Dispose() => Directory.Delete(_desktop, recursive: true);

    private CliHarness Harness(params string?[] input)
    {
        var cli = new CliHarness { DefaultFolder = _desktop };
        cli.Source.AddAppError(1, CliHarness.Now.AddDays(-3));
        foreach (var line in input)
        {
            cli.Input.Enqueue(line);
        }

        return cli;
    }

    // Seven answers to the owner questions, then Enter to close the window.
    private static readonly string?[] Answers = ["1", "1", "2", "7", "4", "1", "Only in one game", ""];

    [Fact]
    public void No_command_saves_a_bundle_to_the_default_folder_with_the_answers()
    {
        var cli = Harness(Answers);

        var exit = cli.Run();

        Assert.Equal(0, exit);
        var bundle = Assert.Single(Directory.GetFiles(_desktop, "*.zip"));
        Assert.StartsWith("sherlog-evidence-", Path.GetFileName(bundle));
        var owner = BundleReader.Open(bundle).Label.Owner;
        Assert.Equal("Playing a game", owner.Doing);
        Assert.Equal("Only in one game", owner.Note);
        var text = cli.Out.ToString();
        Assert.Contains("doesn't change anything on your PC", text);
        Assert.Contains(bundle, text);
    }

    [Fact]
    public void Guided_collect_waits_for_enter_before_closing()
    {
        var cli = Harness(Answers);

        cli.Run();

        Assert.Empty(cli.Input);
        Assert.EndsWith("Press Enter to close.", cli.Out.ToString().TrimEnd());
    }

    [Fact]
    public void Guided_collect_never_overwrites_an_earlier_bundle()
    {
        Harness(Answers).Run();
        Harness(Answers).Run();

        Assert.Equal(2, Directory.GetFiles(_desktop, "*.zip").Length);
    }

    [Fact]
    public void Guided_collect_error_still_waits_for_enter()
    {
        var cli = Harness(Answers);
        cli.DefaultFolder = Path.Combine(_desktop, "missing");

        var exit = cli.Run();

        Assert.Equal(1, exit);
        Assert.Contains("missing", cli.Error.ToString());
        Assert.EndsWith("Press Enter to close.", cli.Out.ToString().TrimEnd());
    }

    [Fact]
    public void Help_and_subcommands_still_work()
    {
        var cli = new CliHarness { DefaultFolder = _desktop };

        Assert.Equal(0, cli.Run("--help"));
        Assert.Contains("collect", cli.Out.ToString());
        Assert.Contains("explain", cli.Out.ToString());
        Assert.Equal(0, new CliHarness { DefaultFolder = _desktop }.Run("recipes", "check"));
        Assert.Empty(Directory.GetFiles(_desktop));
    }
}
