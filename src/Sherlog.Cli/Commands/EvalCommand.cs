using System.CommandLine;
using System.Globalization;
using Sherlog.Cli.Output;
using Sherlog.Core.Evaluation;

namespace Sherlog.Cli.Commands;

/// <summary><c>sherlog eval</c>: score diagnoses against labeled evidence bundles (Gate 1).</summary>
internal static class EvalCommand
{
    public static Command Create(CliEnvironment env)
    {
        var folder = new Argument<string>("folder")
        {
            Description = "A folder of evidence bundles (.zip) with filled-in labels, and/or public case files (.yaml).",
        };
        var library = CommonOptions.Library();
        var json = CommonOptions.Json();
        var command = new Command("eval", "Check how often Sherlog's diagnoses match the labeled causes.") { folder, library, json };

        command.SetAction(result => CliApp.Guard(env, () =>
        {
            var dir = result.GetValue(folder)!;
            if (!Directory.Exists(dir))
            {
                throw new UserErrorException($"There's no such folder: {dir}");
            }

            var bundles = Files(dir, "*.zip");
            var cases = Files(dir, "*.yaml");
            if (bundles.Count == 0 && cases.Count == 0)
            {
                throw new UserErrorException($"No evidence bundles (.zip) or public cases (.yaml) in {dir}.");
            }

            var recipes = Analysis.LoadRecipes(env, result.GetValue(library));
            foreach (var error in recipes.Errors)
            {
                env.Error.WriteLine($"Skipped recipe {Path.GetFileName(error.FilePath)}: {error.Message}");
            }

            var report = Evaluator.Evaluate(bundles, recipes.Recipes, cases);
            foreach (var bad in report.UnreadableBundles)
            {
                env.Error.WriteLine($"Left out {Path.GetFileName(bad.Path)}: {bad.Reason}");
            }

            if (result.GetValue(json))
            {
                JsonOutput.Write(env.Out, report);
            }
            else
            {
                Print(env.Out, report);
            }

            return 0;
        }));
        return command;
    }

    private static void Print(TextWriter w, EvalReport r)
    {
        var bundles = r.BundleCount == 1 ? "1 bundle" : $"{r.BundleCount} bundles";
        w.WriteLine(r.CaseCount == 0
            ? $"Evaluated {bundles}."
            : $"Evaluated {bundles} and {(r.CaseCount == 1 ? "1 public case" : $"{r.CaseCount} public cases")}.");
        w.WriteLine();
        w.WriteLine(r.Seeded.Labeled == 0
            ? "Seeded crashes: none labeled."
            : $"Seeded crashes: {r.Seeded.Correct} of {r.Seeded.Labeled} matched correctly ({Percent(r.Seeded.Correct, r.Seeded.Labeled)}). Gate: 100%.");

        // Gate 1 (ADR 0010): accuracy from public cases, the Unclear share from real PCs, wrong Confirmed from both.
        if (r.CaseCount > 0)
        {
            w.WriteLine();
            // ADR 0014: the gate counts each case once, as one PC; per-problem figures are context.
            var c = r.CaseLevel;
            w.WriteLine($"Public cases that count toward the gate: {c.Cases} (gate needs 30).");
            w.WriteLine($"  Correct: {c.CorrectCases} of {c.ConfidentCases} cases with a Confirmed or Likely answer ({Percent(c.CorrectCases, c.ConfidentCases)}; gate needs 80%, from at least 10 cases).");
            w.WriteLine($"  Wrong Confirmed: {c.WrongConfirmedCases} cases.");
            var g = r.PublicCases.GatePrecision;
            w.WriteLine($"  By problem: {g.ConfidentCorrect} of {g.ConfidentDiagnoses} Confirmed or Likely diagnoses correct.");
            foreach (var (strength, s) in r.PublicCases.ByStrength.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                w.WriteLine($"    {strength}: {s.ConfidentCorrect} of {s.ConfidentDiagnoses} correct; {s.Labeled} labeled.");
            }
            w.WriteLine($"  Unclear: {r.PublicCases.UnclearGroups} of {r.PublicCases.Groups} problems (not a gate line for public cases).");
        }

        w.WriteLine();
        if (r.BundleCount == 0)
        {
            w.WriteLine("Field bundles: none.");
        }
        else
        {
            w.WriteLine($"Field bundles with counted labels: {r.Field.QualifyingBundles}.");
            Accuracy(w, r.Field, gateLine: r.CaseCount == 0);
            w.WriteLine($"  Wrong Confirmed: {r.Field.WrongConfirmed}.");
            w.WriteLine($"  Unclear: {r.Field.UnclearCrashes} of {r.Field.Crashes} crashes ({Percent(r.Field.UnclearCrashes, r.Field.Crashes)}; gate allows 40%), "
                        + $"{r.Field.UnclearGroups} of {r.Field.Groups} problems.");
        }

        w.WriteLine();
        w.WriteLine($"Wrong Confirmed in total: {r.CaseLevel.WrongConfirmedCases + r.Field.WrongConfirmed} (gate allows 1).");

        if (r.Misses.Count > 0)
        {
            w.WriteLine();
            w.WriteLine("Misses");
            foreach (var m in r.Misses)
            {
                w.WriteLine($"  {m.Bundle} {m.CrashGroup}: expected {m.Expected}, got {m.Got ?? "no recipe"} ({m.Confidence})");
            }
        }
    }

    private static void Accuracy(TextWriter w, FieldScore score, bool gateLine)
    {
        var g = score.GatePrecision;
        // ADR 0012: the accuracy line also needs a minimum number of confident answers.
        var gate = gateLine ? "; gate needs 80%, from at least 10 of them" : string.Empty;
        w.WriteLine($"  Correct: {g.ConfidentCorrect} of {g.ConfidentDiagnoses} Confirmed or Likely diagnoses ({Percent(g.ConfidentCorrect, g.ConfidentDiagnoses)}{gate}).");
        foreach (var (strength, s) in score.ByStrength.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            w.WriteLine($"    {strength}: {s.ConfidentCorrect} of {s.ConfidentDiagnoses} correct; {s.Labeled} labeled.");
        }
    }

    private static List<string> Files(string dir, string pattern) =>
        Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase).ToList();

    private static string Percent(int part, int whole) =>
        whole == 0 ? "n/a" : (100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture) + "%";
}
