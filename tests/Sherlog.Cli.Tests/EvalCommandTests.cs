using Sherlog.Core.Bundles;
using Sherlog.Core.Tests;

namespace Sherlog.Cli.Tests;

public sealed class EvalCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sherlog-evalcli-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Bundle(string name, FakeRecordSource source, bool seeded, params CrashLabel[] labels) =>
        BundleWriter.Write(Path.Combine(_dir, name), source, CliHarness.Now.AddDays(-90), CliHarness.System,
            new LabelFile(OwnerAnswers.Empty, new InvestigatorSection(seeded, labels)),
            new Redactor(new RedactionTargets("TESTPC", null, [])), "test", CliHarness.Now);

    [Fact]
    public void Eval_prints_summary_and_misses()
    {
        var resets = new CliHarness();
        resets.AddNvidiaResets(1);
        Bundle("seeded.zip", resets.Source, seeded: true,
            new CrashLabel(null, null, "unknown", null, "seeded"),
            new CrashLabel(null, "Missing.exe", "gpu-nvidia-driver-reset", null, "seeded"));
        Bundle("field.zip", new FakeRecordSource().AddAppError(1, CliHarness.Now.AddDays(-1)), seeded: false,
            new CrashLabel(null, "SherlogTestGame.exe", "gpu-nvidia-driver-reset", "owner said so", "owner-reported"));
        var cli = new CliHarness();

        var exit = cli.Run("eval", _dir, "--library", CliHarness.ValidRecipes);

        Assert.Equal(0, exit);
        var text = cli.Out.ToString();
        Assert.Contains("Evaluated 2 bundles", text);
        Assert.Contains("Seeded crashes:", text);
        Assert.Contains("owner-reported", text);
        Assert.Contains("Unclear:", text);
        Assert.Contains("Misses", text);
        Assert.Contains("field.zip", text);
    }

    [Fact]
    public void Eval_json_has_scores()
    {
        Bundle("field.zip", new FakeRecordSource().AddAppError(1, CliHarness.Now.AddDays(-1)), seeded: false);
        var cli = new CliHarness();

        Assert.Equal(0, cli.Run("eval", _dir, "--json", "--library", CliHarness.ValidRecipes));

        var root = System.Text.Json.JsonDocument.Parse(cli.Out.ToString()).RootElement;
        Assert.Equal(1, root.GetProperty("bundleCount").GetInt32());
        Assert.Equal(1, root.GetProperty("field").GetProperty("groups").GetInt32());
    }

    [Fact]
    public void Eval_folder_without_bundles_exits_1()
    {
        var cli = new CliHarness();

        Assert.Equal(1, cli.Run("eval", _dir, "--library", CliHarness.ValidRecipes));
        Assert.Contains("No .zip", cli.Error.ToString());
    }
}
