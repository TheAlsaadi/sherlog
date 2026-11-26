using Sherlog.Core.Bundles;
using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Evaluation;

/// <summary>
/// Re-runs diagnosis on labeled bundles and public cases and scores it against Gate 1 (plan.md, ADRs 0006, 0010):
/// seeded, field and public-case results apart; accuracy only from labels that are confirmed-by-fix or owner-reported;
/// everything else is coverage.
/// </summary>
public static class Evaluator
{
    private const string NoCrash = "(no crash found)";

    // Public cases are placed in time here; only the spacing between their crashes matters.
    private static readonly DateTimeOffset CaseEnd = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly HashSet<string> CountedStrengths = new(StringComparer.Ordinal) { "confirmed-by-fix", "owner-reported" };

    public static EvalReport Evaluate(IEnumerable<string> bundlePaths, IReadOnlyList<Recipe> recipes, IEnumerable<string>? casePaths = null)
    {
        var diagnoser = new Diagnoser(recipes, TimeZoneInfo.Utc);
        var misses = new List<EvalMiss>();
        var unreadable = new List<UnreadableBundle>();
        var field = new Tally(misses);
        var cases = new Tally(misses);
        int seededLabeled = 0, seededCorrect = 0, bundles = 0, caseCount = 0;

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
            var labels = bundle.Label.Investigator.Labels
                .Where(l => l.TrueCause != "unknown" && (l.GroupId ?? l.AppName) is not null);

            if (!bundle.Label.Investigator.Seeded)
            {
                field.Score(name, diagnoses, labels.Select(l => (l, Resolve(l, diagnoses))));
                continue;
            }

            foreach (var label in labels)
            {
                var matched = Resolve(label, diagnoses);
                if (matched.Count == 0)
                {
                    misses.Add(new EvalMiss(name, label.GroupId ?? NoCrash, label.TrueCause, null, ConfidenceLevel.Unclear));
                }

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
            }
        }

        foreach (var path in casePaths ?? [])
        {
            PublicCase c;
            try
            {
                c = PublicCaseFile.Load(path);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                unreadable.Add(new UnreadableBundle(path, ex.Message));
                continue;
            }

            caseCount++;

            // A case describes one problem, so its label applies to every crash group the case produces.
            var diagnoses = diagnoser.DiagnoseAll(CrashGrouper.Group(c.ToCrashes(CaseEnd)));
            var label = new CrashLabel(null, null, c.TrueCause, c.Evidence, c.LabelStrength);
            cases.Score(Path.GetFileName(path), diagnoses, [(label, diagnoses.ToList())]);
        }

        return new EvalReport(
            new SeededScore(seededLabeled, seededCorrect),
            field.Result(),
            misses,
            bundles,
            unreadable,
            cases.Result(),
            caseCount);
    }

    // A label names its crash group by id, or by app name (every group whose latest crash is that app).
    private static List<Diagnosis.Diagnosis> Resolve(CrashLabel label, IReadOnlyList<Diagnosis.Diagnosis> diagnoses) =>
        diagnoses.Where(d => label.GroupId is not null
                ? string.Equals(d.Group.Id, label.GroupId, StringComparison.OrdinalIgnoreCase)
                : string.Equals(d.Group.Latest.AppName, label.AppName, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static EvalMiss Miss(string source, CrashLabel label, Diagnosis.Diagnosis d) =>
        new(source, d.Group.Id, label.TrueCause, d.Recipe?.Id, d.Confidence);

    /// <summary>Scores of one kind of real-world evidence (field bundles or public cases).</summary>
    private sealed class Tally(List<EvalMiss> misses)
    {
        private readonly Dictionary<string, StrengthScore> _byStrength = new(StringComparer.Ordinal);
        private int _wrongConfirmed, _qualifying, _groups, _unclearGroups, _crashes, _unclearCrashes;

        public void Score(string name, IReadOnlyList<Diagnosis.Diagnosis> diagnoses,
            IEnumerable<(CrashLabel Label, List<Diagnosis.Diagnosis> Matched)> labels)
        {
            _groups += diagnoses.Count;
            _crashes += diagnoses.Sum(d => d.Group.Crashes.Count);
            var unclear = diagnoses.Where(d => d.Confidence == ConfidenceLevel.Unclear).ToList();
            _unclearGroups += unclear.Count;
            _unclearCrashes += unclear.Sum(d => d.Group.Crashes.Count);

            var qualifies = false;
            foreach (var (label, matched) in labels.Where(x => CountedStrengths.Contains(x.Label.LabelStrength)))
            {
                if (matched.Count == 0)
                {
                    misses.Add(new EvalMiss(name, label.GroupId ?? NoCrash, label.TrueCause, null, ConfidenceLevel.Unclear));
                }

                qualifies = true;
                var score = _byStrength.GetValueOrDefault(label.LabelStrength, new StrengthScore(0, 0, 0));
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
                        _wrongConfirmed += d.Confidence == ConfidenceLevel.Confirmed ? 1 : 0;
                    }
                }

                _byStrength[label.LabelStrength] = score;
            }

            _qualifying += qualifies ? 1 : 0;
        }

        public FieldScore Result()
        {
            var gate = _byStrength.Values.Aggregate(new StrengthScore(0, 0, 0), (a, b) =>
                new StrengthScore(a.Labeled + b.Labeled, a.ConfidentDiagnoses + b.ConfidentDiagnoses, a.ConfidentCorrect + b.ConfidentCorrect));
            return new FieldScore(new Dictionary<string, StrengthScore>(_byStrength), gate, _wrongConfirmed, _groups, _unclearGroups,
                _crashes, _unclearCrashes, _qualifying);
        }
    }
}
