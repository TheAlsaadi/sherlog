using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evaluation;
using Sherlog.Core.Evidence;
using Sherlog.Core.Recipes;
using static Sherlog.Core.Tests.Diagnosis.Make;

namespace Sherlog.Core.Tests.Evaluation;

public sealed class PublicCaseTests : IDisposable
{
    private const string NvidiaCase = """
        format: 1
        id: pc-0001
        source_url: https://example.invalid/thread/1
        retrieved: 2026-11-27
        windows: Windows 11 24H2
        crashes:
          - source: display-reset
            faulting_module: nvlddmkm
            count: 4
            days: 3
        truth:
          true_cause: nvidia
          label_strength: confirmed-by-fix
          evidence: Rolled back the driver; no resets for two weeks.
        """;

    private static readonly Recipe Overlay = Recipe("overlay", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")]);
    private static readonly Recipe Nvidia = Recipe("nvidia", [When(EvidenceSources.DisplayReset, module: "nvlddmkm")],
        confirmedWhen: new RepeatRule(3, 7));
    private static readonly IReadOnlyList<Recipe> Recipes = [Overlay, Nvidia];

    private readonly string _dir = Directory.CreateTempSubdirectory("sherlog-cases-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string yaml)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, yaml);
        return path;
    }

    [Fact]
    public void Case_file_is_read_with_codes_normalized()
    {
        var path = Write("pc-0002.yaml", """
            format: 1
            id: pc-0002
            source_url: https://example.invalid/thread/2
            crashes:
              - source: application-error
                app_name: SherlogTestGame.exe
                faulting_module: FakeOverlay64.dll
                exception_code: C0000005
              - source: bugcheck
                bugcheck_code: "0x116"
            truth:
              true_cause: overlay
              label_strength: owner-reported
              evidence: Turned the overlay off and it stopped.
            """);

        var c = PublicCaseFile.Load(path);

        Assert.Equal("pc-0002", c.Id);
        Assert.Equal("https://example.invalid/thread/2", c.SourceUrl);
        Assert.Equal("overlay", c.TrueCause);
        Assert.Equal("owner-reported", c.LabelStrength);
        Assert.Equal(2, c.Crashes.Count);
        Assert.Equal("0xc0000005", c.Crashes[0].ExceptionCode);
        Assert.Equal("0x00000116", c.Crashes[1].BugCheckCode);
        Assert.Equal(1, c.Crashes[0].Count);
    }

    [Theory]
    [InlineData("source_url: https://example.invalid/t", "source_url: ftp://example.invalid/t", "source_url")]
    [InlineData("source: display-reset", "source: reliability", "source")]
    [InlineData("label_strength: confirmed-by-fix", "label_strength: unknown", "label_strength")]
    [InlineData("count: 4", "count: 0", "count")]
    [InlineData("true_cause: nvidia", "true_cause: \"\"", "true_cause")]
    [InlineData("id: pc-0001", "id: ../escape", "id")]
    public void Bad_case_files_are_refused_with_the_field_named(string find, string replace, string field)
    {
        var yaml = NvidiaCase.Replace("https://example.invalid/thread/1", "https://example.invalid/t", StringComparison.Ordinal)
            .Replace(find, replace, StringComparison.Ordinal);
        Assert.NotEqual(NvidiaCase, yaml);
        var path = Write("bad.yaml", yaml);

        var ex = Assert.Throws<InvalidDataException>(() => PublicCaseFile.Load(path));

        Assert.Contains(field, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Crash_entries_become_events_spread_over_their_days()
    {
        var c = PublicCaseFile.Load(Write("pc-0001.yaml", NvidiaCase));

        var crashes = c.ToCrashes(T0);

        Assert.Equal(4, crashes.Count);
        Assert.Equal(4, crashes.Select(x => x.Id).Distinct().Count());
        Assert.All(crashes, x => Assert.Equal(4101, x.EventId));
        Assert.All(crashes, x => Assert.Equal("Display", x.Provider));
        Assert.Equal(T0, crashes.Max(x => x.Time));
        Assert.Equal(T0.AddDays(-2), crashes.Min(x => x.Time));
    }

    [Fact]
    public void Eval_scores_public_cases_apart_from_field_bundles()
    {
        var right = Write("pc-0001.yaml", NvidiaCase);
        var wrong = Write("pc-0003.yaml", NvidiaCase
            .Replace("id: pc-0001", "id: pc-0003", StringComparison.Ordinal)
            .Replace("true_cause: nvidia", "true_cause: none:faulty-power-supply", StringComparison.Ordinal));
        var unclear = Write("pc-0004.yaml", NvidiaCase
            .Replace("id: pc-0001", "id: pc-0004", StringComparison.Ordinal)
            .Replace("faulting_module: nvlddmkm", "faulting_module: SomeOtherDriver", StringComparison.Ordinal));

        var report = Evaluator.Evaluate([], Recipes, [right, wrong, unclear]);

        Assert.Equal(3, report.CaseCount);
        var cases = report.PublicCases;
        Assert.Equal(3, cases.QualifyingBundles);
        Assert.Equal(new StrengthScore(Labeled: 3, ConfidentDiagnoses: 2, ConfidentCorrect: 1), cases.GatePrecision);
        Assert.Equal(1, cases.WrongConfirmed);
        Assert.Equal(1, cases.UnclearGroups);
        // Both the wrong answer and the Unclear one are listed, so the gate file can show every miss.
        Assert.Equal(["pc-0003.yaml", "pc-0004.yaml"], report.Misses.Select(m => m.Bundle).Order(StringComparer.Ordinal));
        Assert.Equal(ConfidenceLevel.Confirmed, report.Misses.Single(m => m.Bundle == "pc-0003.yaml").Confidence);
        Assert.Null(report.Misses.Single(m => m.Bundle == "pc-0004.yaml").Got);

        // Public cases never count toward the field figures, including the Unclear share.
        Assert.Equal(0, report.Field.Crashes);
        Assert.Equal(0, report.BundleCount);
    }

    [Fact]
    public void Every_committed_case_file_loads_and_is_named_after_its_id()
    {
        var root = Repo.Path("tests", "public-cases");
        var problems = (
            from file in Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
            let error = Check(file)
            where error is not null
            select $"{Path.GetRelativePath(root, file)}: {error}").ToList();

        Assert.Empty(problems);

        static string? Check(string file)
        {
            try
            {
                var c = PublicCaseFile.Load(file);
                return c.Id == Path.GetFileNameWithoutExtension(file) ? null : $"id {c.Id} doesn't match the file name";
            }
            catch (InvalidDataException ex)
            {
                return ex.Message;
            }
        }
    }

    [Fact]
    public void Gate_counts_each_case_once_and_right_only_if_every_confident_answer_is_right()
    {
        // ADR 0014: one PC with several problems is one answer to the person, right or wrong as a whole.
        const string overlay = """
              - source: application-error
                app_name: SherlogTestGame.exe
                faulting_module: FakeOverlay64.dll
                exception_code: "0xc0000005"
            """;
        var right = Write("pc-0001.yaml", NvidiaCase);
        var partlyWrong = Write("pc-0005.yaml", NvidiaCase
            .Replace("id: pc-0001", "id: pc-0005", StringComparison.Ordinal)
            .Replace("    days: 3", "    days: 3\n" + overlay.TrimEnd(), StringComparison.Ordinal));
        var wrong = Write("pc-0006.yaml", NvidiaCase
            .Replace("id: pc-0001", "id: pc-0006", StringComparison.Ordinal)
            .Replace("true_cause: nvidia", "true_cause: none:faulty-power-supply", StringComparison.Ordinal));
        var unclear = Write("pc-0004.yaml", NvidiaCase
            .Replace("id: pc-0001", "id: pc-0004", StringComparison.Ordinal)
            .Replace("faulting_module: nvlddmkm", "faulting_module: SomeOtherDriver", StringComparison.Ordinal));
        Assert.Equal(2, PublicCaseFile.Load(partlyWrong).Crashes.Count);

        var report = Evaluator.Evaluate([], Recipes, [right, partlyWrong, wrong, unclear]);

        Assert.Equal(new CaseScore(Cases: 4, ConfidentCases: 3, CorrectCases: 1, WrongConfirmedCases: 1), report.CaseLevel);
        // By problem, the partly wrong case adds two confident answers, one of them right.
        Assert.Equal(new StrengthScore(Labeled: 5, ConfidentDiagnoses: 4, ConfidentCorrect: 2), report.PublicCases.GatePrecision);
    }

    [Fact]
    public void Unreadable_case_files_are_listed_not_fatal()
    {
        var good = Write("pc-0001.yaml", NvidiaCase);
        var bad = Write("broken.yaml", "format: 1\nid: [unclosed");

        var report = Evaluator.Evaluate([], Recipes, [good, bad]);

        Assert.Equal(1, report.CaseCount);
        Assert.Equal(bad, Assert.Single(report.UnreadableBundles).Path);
    }
}
