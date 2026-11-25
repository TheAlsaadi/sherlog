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
        var folder = new Argument<string>("folder") { Description = "A folder of evidence bundles (.zip) with filled-in labels." };
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

            var bundles = Directory.EnumerateFiles(dir, "*.zip", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (bundles.Count == 0)
            {
                throw new UserErrorException($"No .zip evidence bundles in {dir}.");
            }

            var recipes = Analysis.LoadRecipes(env, result.GetValue(library));
            foreach (var error in recipes.Errors)
            {
                env.Error.WriteLine($"Skipped recipe {Path.GetFileName(error.FilePath)}: {error.Message}");
            }

            var report = Evaluator.Evaluate(bundles, recipes.Recipes);
            foreach (var bad in report.UnreadableBundles)
            {
                env.Error.WriteLine($"Couldn't read {Path.GetFileName(bad)}; it was left out.");
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
        w.WriteLine($"Evaluated {r.BundleCount} bundles.");
        w.WriteLine();
        w.WriteLine(r.Seeded.Labeled == 0
            ? "Seeded crashes: none labeled."
            : $"Seeded crashes: {r.Seeded.Correct} of {r.Seeded.Labeled} matched correctly ({Percent(r.Seeded.Correct, r.Seeded.Labeled)}). Gate: 100%.");
        w.WriteLine();
        w.WriteLine($"Field bundles that count toward the gate: {r.Field.QualifyingBundles} (gate needs 10).");
        foreach (var (strength, s) in r.Field.ByStrength.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            w.WriteLine($"  {strength}: {s.ConfidentCorrect} of {s.ConfidentDiagnoses} Confirmed or Likely diagnoses correct ({Percent(s.ConfidentCorrect, s.ConfidentDiagnoses)}); {s.Labeled} labeled.");
        }

        w.WriteLine($"  Wrong Confirmed: {r.Field.WrongConfirmed} (gate allows 1).");
        w.WriteLine($"  Unclear: {r.Field.UnclearGroups} of {r.Field.Groups} problems ({Percent(r.Field.UnclearGroups, r.Field.Groups)}; gate allows 40%).");

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

    private static string Percent(int part, int whole) =>
        whole == 0 ? "n/a" : (100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture) + "%";
}
