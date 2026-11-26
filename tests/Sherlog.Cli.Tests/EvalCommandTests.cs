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
    public void Eval_says_1_bundle_not_1_bundles()
    {
        Bundle("field.zip", new FakeRecordSource().AddAppError(1, CliHarness.Now.AddDays(-1)), seeded: false);
        var cli = new CliHarness();

        Assert.Equal(0, cli.Run("eval", _dir, "--library", CliHarness.ValidRecipes));
        Assert.Contains("Evaluated 1 bundle.", cli.Out.ToString());
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
        Assert.Contains("No evidence bundles (.zip) or public cases (.yaml)", cli.Error.ToString());
    }

    [Fact]
    public void Eval_scores_public_cases_in_the_folder()
    {
        File.WriteAllText(Path.Combine(_dir, "pc-0001.yaml"), """
            format: 1
            id: pc-0001
            source_url: https://example.invalid/thread/1
            crashes:
              - source: display-reset
                faulting_module: nvlddmkm
                count: 3
                days: 2
            truth:
              true_cause: gpu-nvidia-driver-reset
              label_strength: owner-reported
            """);
        File.WriteAllText(Path.Combine(_dir, "README.md"), "Not a case.");
        var cli = new CliHarness();

        Assert.Equal(0, cli.Run("eval", _dir, "--library", CliHarness.ValidRecipes));

        var text = cli.Out.ToString();
        Assert.Contains("Evaluated 0 bundles and 1 public case.", text);
        Assert.Contains("Public cases that count toward the gate: 1 (gate needs 30).", text);
        Assert.Contains("Correct: 1 of 1 Confirmed or Likely diagnoses (100%; gate needs 80%).", text);
        Assert.Contains("Field bundles: none.", text);
    }
}
