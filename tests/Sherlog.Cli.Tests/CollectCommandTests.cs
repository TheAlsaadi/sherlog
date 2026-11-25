using Sherlog.Core.Bundles;
using Sherlog.Core.Tests;

namespace Sherlog.Cli.Tests;

public sealed class CollectCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sherlog-collect-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Out => Path.Combine(_dir, "evidence.zip");

    [Fact]
    public void Collect_no_questions_writes_prefilled_labels()
    {
        var cli = new CliHarness();
        cli.Source.AddAppError(1, CliHarness.Now.AddDays(-3));
        cli.AddNvidiaResets(2);

        var exit = cli.Run("collect", "--redact", "--no-questions", "--out", Out);

        Assert.Equal(0, exit);
        var bundle = BundleReader.Open(Out);
        Assert.Equal(2, bundle.Investigator().Labels.Count);
        Assert.All(bundle.Investigator().Labels, l =>
        {
            Assert.StartsWith("g-", l.GroupId);
            Assert.Equal("unknown", l.TrueCause);
            Assert.Equal("unknown", l.LabelStrength);
        });
        Assert.Contains(bundle.Investigator().Labels, l => l.AppName == "SherlogTestGame.exe");
        Assert.Equal(OwnerAnswers.Empty, bundle.Label.Owner);
        Assert.Equal(CliHarness.Now, bundle.Manifest.CreatedUtc);
        Assert.Equal(CliHarness.Now.AddDays(-90), bundle.Manifest.SinceUtc);
        Assert.Equal("Test CPU", bundle.System.Cpu);
    }

    [Fact]
    public void Collect_asks_questions_and_records_answers()
    {
        var cli = new CliHarness();
        cli.Source.AddAppError(1, CliHarness.Now.AddDays(-3));
        foreach (var answer in new[] { "1", "1", "2", "7", "4", "1", "Only in one game" })
        {
            cli.Input.Enqueue(answer);
        }

        Assert.Equal(0, cli.Run("collect", "--out", Out));

        var owner = BundleReader.Open(Out).Label.Owner;
        Assert.StartsWith("SherlogTestGame.exe", owner.WhatCrashed);
        Assert.Equal("Playing a game", owner.Doing);
        Assert.Equal(["Windows update"], owner.RecentChanges);
        Assert.Equal(["Nothing yet"], owner.Tried);
        Assert.Equal("I haven't tried anything", owner.Helped);
        Assert.Equal("Yes, often", owner.StillHappens);
        Assert.Equal("Only in one game", owner.Note);
    }

    [Fact]
    public void Collect_redacts_with_this_pcs_targets_and_says_so()
    {
        var cli = new CliHarness();
        cli.Source.AddAppError(1, CliHarness.Now.AddDays(-3));
        cli.Source.Events[0] = cli.Source.Events[0] with { Message = @"Path C:\Users\TESTUSER\x.exe on TESTPC" };

        cli.Run("collect", "--no-questions", "--out", Out);

        var message = BundleReader.Open(Out).Records.ReadEvents(DateTimeOffset.MinValue).Single().Message;
        Assert.Equal(@"Path C:\Users\USER\x.exe on PC", message);
        var text = cli.Out.ToString();
        Assert.Contains(Path.GetFullPath(Out), text);
        Assert.Contains("replaced your user name, your PC's name and your account ID", text);
    }

    [Fact]
    public void Collect_to_a_missing_folder_fails_before_asking_anything()
    {
        var cli = new CliHarness();
        cli.Source.AddAppError(1, CliHarness.Now.AddDays(-3));
        cli.Input.Enqueue("1");
        var target = Path.Combine(_dir, "missing", "evidence.zip");

        var exit = cli.Run("collect", "--out", target);

        Assert.Equal(1, exit);
        Assert.Single(cli.Input);
        Assert.Contains(Path.Combine(_dir, "missing"), cli.Error.ToString());
    }

    [Fact]
    public void Collect_to_a_folder_path_fails_and_leaves_nothing_behind()
    {
        var cli = new CliHarness();

        var exit = cli.Run("collect", "--no-questions", "--out", _dir);

        Assert.Equal(1, exit);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_dir)!, Path.GetFileName(_dir) + "*.partial"));
    }

    [Fact]
    public void Crashes_with_a_folder_as_bundle_exits_1()
    {
        var cli = new CliHarness();

        Assert.Equal(1, cli.Run("crashes", "--bundle", _dir, "--library", CliHarness.ValidRecipes));
    }

    [Fact]
    public void Crashes_reads_a_bundle_counting_days_from_when_it_was_made()
    {
        // A bundle made 200 days ago, with a crash 5 days before it was made.
        var made = CliHarness.Now.AddDays(-200);
        var source = new FakeRecordSource().AddAppError(1, made.AddDays(-5));
        BundleWriter.Write(Out, source, made.AddDays(-90), CliHarness.System,
            new LabelFile(OwnerAnswers.Empty, new InvestigatorSection(false, [])),
            new Redactor(new RedactionTargets("TESTPC", null, [])), "test", made);
        var cli = new CliHarness();

        Assert.Equal(0, cli.Run("crashes", "--bundle", Out, "--library", CliHarness.ValidRecipes));

        Assert.Contains("SherlogTestGame.exe", cli.Out.ToString());
    }
}

internal static class BundleReaderExtensions
{
    public static InvestigatorSection Investigator(this BundleReader bundle) => bundle.Label.Investigator;
}
