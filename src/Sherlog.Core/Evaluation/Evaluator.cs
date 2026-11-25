using Sherlog.Core.Bundles;
using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Evaluation;

/// <summary>
/// Re-runs diagnosis on labeled bundles and scores it against Gate 1 (plan.md, ADR 0006): seeded and field results
/// apart; field accuracy only from labels that are confirmed-by-fix or owner-reported; everything else is coverage.
/// </summary>
public static class Evaluator
{
    private const string NoCrash = "(no crash found)";

    private static readonly HashSet<string> CountedStrengths = new(StringComparer.Ordinal) { "confirmed-by-fix", "owner-reported" };

    public static EvalReport Evaluate(IEnumerable<string> bundlePaths, IReadOnlyList<Recipe> recipes)
    {
        var diagnoser = new Diagnoser(recipes, TimeZoneInfo.Utc);
        var misses = new List<EvalMiss>();
        var unreadable = new List<UnreadableBundle>();
        var byStrength = new Dictionary<string, StrengthScore>(StringComparer.Ordinal);
        int seededLabeled = 0, seededCorrect = 0, wrongConfirmed = 0, qualifying = 0, bundles = 0;
        int groups = 0, unclearGroups = 0, crashes = 0, unclearCrashes = 0;

        foreach (var path in bundlePaths)
        {
            BundleReader bundle;
            try
            {
                bundle = BundleReader.Open(path);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                unreadable.Add(new UnreadableBundle(path, ex.Message));
                continue;
            }

            bundles++;
            var name = Path.GetFileName(path);
            var diagnoses = diagnoser.DiagnoseAll(CrashGrouper.Group(new EvidenceReader(bundle.Records).ReadCrashes(DateTimeOffset.MinValue)));
            var seeded = bundle.Label.Investigator.Seeded;
            if (!seeded)
            {
                groups += diagnoses.Count;
                crashes += diagnoses.Sum(d => d.Group.Crashes.Count);
                var unclear = diagnoses.Where(d => d.Confidence == ConfidenceLevel.Unclear).ToList();
                unclearGroups += unclear.Count;
                unclearCrashes += unclear.Sum(d => d.Group.Crashes.Count);
            }

            var labels = bundle.Label.Investigator.Labels
                .Where(l => l.TrueCause != "unknown" && (l.GroupId ?? l.AppName) is not null)
                .Where(l => seeded || CountedStrengths.Contains(l.LabelStrength));
            var qualifies = false;
            foreach (var label in labels)
            {
                var matched = Resolve(label, diagnoses);
                if (matched.Count == 0)
                {
                    misses.Add(new EvalMiss(name, label.GroupId ?? NoCrash, label.TrueCause, null, ConfidenceLevel.Unclear));
                }

                if (seeded)
                {
                    seededLabeled += Math.Max(1, matched.Count);
                    foreach (var d in matched)
                    {
                        if (d.Recipe?.Id == label.TrueCause)
                        {
                            seededCorrect++;
                        }
                        else
                        {
                            misses.Add(Miss(name, label, d));
                        }
                    }

                    continue;
                }

                qualifies = true;
                var score = byStrength.GetValueOrDefault(label.LabelStrength, new StrengthScore(0, 0, 0));
                foreach (var d in matched.DefaultIfEmpty())
                {
                    var confident = d is { Confidence: ConfidenceLevel.Confirmed or ConfidenceLevel.Likely };
                    var correct = d?.Recipe?.Id == label.TrueCause;
                    score = new StrengthScore(
                        score.Labeled + 1,
                        score.ConfidentDiagnoses + (confident ? 1 : 0),
                        score.ConfidentCorrect + (confident && correct ? 1 : 0));
                    if (d is not null && !correct)
                    {
                        misses.Add(Miss(name, label, d));
                        wrongConfirmed += d.Confidence == ConfidenceLevel.Confirmed ? 1 : 0;
                    }
                }

                byStrength[label.LabelStrength] = score;
            }

            qualifying += qualifies ? 1 : 0;
        }

        var gate = byStrength.Values.Aggregate(new StrengthScore(0, 0, 0), (a, b) =>
            new StrengthScore(a.Labeled + b.Labeled, a.ConfidentDiagnoses + b.ConfidentDiagnoses, a.ConfidentCorrect + b.ConfidentCorrect));

        return new EvalReport(
            new SeededScore(seededLabeled, seededCorrect),
            new FieldScore(byStrength, gate, wrongConfirmed, groups, unclearGroups, crashes, unclearCrashes, qualifying),
            misses,
            bundles,
            unreadable);
    }

    // A label names its crash group by id, or by app name (every group whose latest crash is that app).
    private static List<Diagnosis.Diagnosis> Resolve(CrashLabel label, IReadOnlyList<Diagnosis.Diagnosis> diagnoses) =>
        diagnoses.Where(d => label.GroupId is not null
                ? string.Equals(d.Group.Id, label.GroupId, StringComparison.OrdinalIgnoreCase)
                : string.Equals(d.Group.Latest.AppName, label.AppName, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static EvalMiss Miss(string bundle, CrashLabel label, Diagnosis.Diagnosis d) =>
        new(bundle, d.Group.Id, label.TrueCause, d.Recipe?.Id, d.Confidence);
}
