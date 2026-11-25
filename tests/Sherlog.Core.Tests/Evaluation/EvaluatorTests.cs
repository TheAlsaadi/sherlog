using Sherlog.Core.Bundles;
using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evaluation;
using Sherlog.Core.Evidence;
using Sherlog.Core.Recipes;
using static Sherlog.Core.Tests.Diagnosis.Make;

namespace Sherlog.Core.Tests.Evaluation;

public sealed class EvaluatorTests : IDisposable
{
    private static readonly Recipe Overlay = Recipe("overlay", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")]);
    private static readonly Recipe Nvidia = Recipe("nvidia", [When(EvidenceSources.DisplayReset, module: "nvlddmkm")],
        confirmedWhen: new RepeatRule(3, 7));
    private static readonly IReadOnlyList<Recipe> Recipes = [Overlay, Nvidia];

    private readonly string _dir = Directory.CreateTempSubdirectory("sherlog-eval-").FullName;
    private int _n;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Bundle(FakeRecordSource source, bool seeded, params CrashLabel[] labels)
    {
        var path = Path.Combine(_dir, $"bundle-{++_n}.zip");
        BundleWriter.Write(path, source, T0.AddDays(-90), new SystemSummary("W", "1", "C", 1, []),
            new LabelFile(OwnerAnswers.Empty, new InvestigatorSection(seeded, labels)),
            new Redactor(new RedactionTargets("TESTPC", null, [])), "test", T0);
        return path;
    }

    private static FakeRecordSource Resets(int count)
    {
        var source = new FakeRecordSource();
        var xml = Fixture.Text("events", "display-4101.xml");
        for (var i = 0; i < count; i++)
        {
            source.Events.Add(new RawEventRecord("System", xml
                .Replace("2026-11-22T20:15:00Z", T0.AddDays(-i).UtcDateTime.ToString("o"))
                .Replace("<EventRecordID>7001<", $"<EventRecordID>{7100 + i}<"), null));
        }

        return source;
    }

    private static CrashLabel ByApp(string app, string cause, string strength) => new(null, app, cause, null, strength);

    [Fact]
    public void Seeded_scores_correct_matches()
    {
        var path = Bundle(new FakeRecordSource().AddAppError(1, T0.AddDays(-1)), seeded: true,
            ByApp("SherlogTestGame.exe", "overlay", "seeded"),
            ByApp("Missing.exe", "overlay", "seeded"));

        var report = Evaluator.Evaluate([path], Recipes);

        Assert.Equal(new SeededScore(Labeled: 2, Correct: 1), report.Seeded);
        var miss = Assert.Single(report.Misses);
        Assert.Equal("overlay", miss.Expected);
        Assert.Null(miss.Got);
        Assert.Equal(0, report.Field.Groups);
    }

    [Fact]
    public void Field_precision_split_by_strength()
    {
        var source = Resets(1).AddAppError(1, T0.AddDays(-1));
        var path = Bundle(source, seeded: false,
            ByApp("SherlogTestGame.exe", "overlay", "owner-reported"),
            new CrashLabel(null, null, "unknown", null, "unknown"));
        var groupId = Sherlog.Core.Grouping.CrashGrouper.Group(new EvidenceReader(source).ReadCrashes(DateTimeOffset.MinValue))
            .Single(g => g.Latest.Source == EvidenceSources.DisplayReset).Id;
        var path2 = Bundle(Resets(1), seeded: false, new CrashLabel(groupId, null, "something-else", "fix worked", "confirmed-by-fix"));

        var report = Evaluator.Evaluate([path, path2], Recipes);

        Assert.Equal(new StrengthScore(Labeled: 1, ConfidentDiagnoses: 1, ConfidentCorrect: 1), report.Field.ByStrength["owner-reported"]);
        Assert.Equal(new StrengthScore(Labeled: 1, ConfidentDiagnoses: 1, ConfidentCorrect: 0), report.Field.ByStrength["confirmed-by-fix"]);
        Assert.Equal(2, report.Field.QualifyingBundles);
        Assert.Contains(report.Misses, m => m.Expected == "something-else" && m.Got == "nvidia" && m.Confidence == ConfidenceLevel.Likely);
    }

    [Fact]
    public void Unknown_labels_only_count_toward_coverage()
    {
        var path = Bundle(new FakeRecordSource().AddAppError(1, T0).AddAppError(2, T0, module: "Other.dll"), seeded: false,
            ByApp("SherlogTestGame.exe", "unknown", "unknown"));

        var report = Evaluator.Evaluate([path], Recipes);

        Assert.Empty(report.Field.ByStrength);
        Assert.Equal(2, report.Field.Groups);
        Assert.Equal(1, report.Field.UnclearGroups);
        Assert.Equal(0, report.Field.QualifyingBundles);
        Assert.Empty(report.Misses);
    }

    [Fact]
    public void Wrong_confirmed_is_counted()
    {
        var path = Bundle(Resets(3), seeded: false, ByApp("unused.exe", "unknown", "unknown"),
            new CrashLabel(null, null, "unknown", null, "unknown"));
        var groupId = Assert.Single(Sherlog.Core.Grouping.CrashGrouper.Group(
            new EvidenceReader(Resets(3)).ReadCrashes(DateTimeOffset.MinValue))).Id;
        var labeled = Bundle(Resets(3), seeded: false, new CrashLabel(groupId, null, "overheating", null, "owner-reported"));

        var report = Evaluator.Evaluate([path, labeled], Recipes);

        Assert.Equal(1, report.Field.WrongConfirmed);
        Assert.Contains(report.Misses, m => m.Confidence == ConfidenceLevel.Confirmed);
    }

    [Fact]
    public void Unclear_diagnosis_of_a_known_cause_is_a_miss_but_not_in_precision()
    {
        var path = Bundle(new FakeRecordSource().AddAppError(1, T0, module: "Other.dll"), seeded: false,
            ByApp("SherlogTestGame.exe", "overlay", "owner-reported"));

        var report = Evaluator.Evaluate([path], Recipes);

        Assert.Equal(new StrengthScore(1, 0, 0), report.Field.ByStrength["owner-reported"]);
        Assert.Single(report.Misses);
    }

    [Fact]
    public void Unreadable_bundle_is_reported_with_its_reason()
    {
        var bad = Path.Combine(_dir, "bad.zip");
        File.WriteAllText(bad, "not a zip");
        var good = Bundle(new FakeRecordSource().AddAppError(1, T0), seeded: false);

        var report = Evaluator.Evaluate([bad, good], Recipes);

        Assert.Equal(1, report.BundleCount);
        var unreadable = Assert.Single(report.UnreadableBundles);
        Assert.Equal("bad.zip", Path.GetFileName(unreadable.Path));
        Assert.False(string.IsNullOrWhiteSpace(unreadable.Reason));
    }

    [Fact]
    public void Labels_of_unknown_strength_are_left_out_of_accuracy()
    {
        var groupId = Assert.Single(Sherlog.Core.Grouping.CrashGrouper.Group(
            new EvidenceReader(Resets(3)).ReadCrashes(DateTimeOffset.MinValue))).Id;
        var path = Bundle(Resets(3), seeded: false, new CrashLabel(groupId, null, "overheating", "a guess", "unknown"));

        var report = Evaluator.Evaluate([path], Recipes);

        Assert.Empty(report.Field.ByStrength);
        Assert.Equal(0, report.Field.WrongConfirmed);
        Assert.Equal(0, report.Field.QualifyingBundles);
        Assert.Equal(1, report.Field.Groups);
    }

    [Fact]
    public void Gate_precision_combines_confirmed_by_fix_and_owner_reported()
    {
        var a = Bundle(new FakeRecordSource().AddAppError(1, T0), seeded: false, ByApp("SherlogTestGame.exe", "overlay", "owner-reported"));
        var b = Bundle(new FakeRecordSource().AddAppError(1, T0), seeded: false, ByApp("SherlogTestGame.exe", "overlay", "confirmed-by-fix"));
        var c = Bundle(new FakeRecordSource().AddAppError(1, T0), seeded: false, ByApp("SherlogTestGame.exe", "nvidia", "owner-reported"));

        var report = Evaluator.Evaluate([a, b, c], Recipes);

        Assert.Equal(new StrengthScore(Labeled: 3, ConfidentDiagnoses: 3, ConfidentCorrect: 2), report.Field.GatePrecision);
    }

    [Fact]
    public void Unclear_share_is_counted_over_crashes_as_well_as_groups()
    {
        // One recognized group with 3 crashes, one unrecognized group with 1 crash.
        var source = Resets(3).AddAppError(1, T0, module: "Other.dll");
        var path = Bundle(source, seeded: false);

        var report = Evaluator.Evaluate([path], Recipes);

        Assert.Equal((2, 1), (report.Field.Groups, report.Field.UnclearGroups));
        Assert.Equal((4, 1), (report.Field.Crashes, report.Field.UnclearCrashes));
    }
}
