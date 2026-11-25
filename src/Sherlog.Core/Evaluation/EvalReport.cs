using Sherlog.Core.Diagnosis;

namespace Sherlog.Core.Evaluation;

/// <summary>How well diagnoses match labels across a set of bundles (Gate 1, plan.md).</summary>
public sealed record EvalReport(
    SeededScore Seeded,
    FieldScore Field,
    IReadOnlyList<EvalMiss> Misses,
    int BundleCount,
    IReadOnlyList<UnreadableBundle> UnreadableBundles);

/// <summary>A bundle that was left out, and why (for example a hand-edited label.yaml with a mistake).</summary>
public sealed record UnreadableBundle(string Path, string Reason);

/// <summary>Seeded crashes: <see cref="Correct"/> = the matched recipe is the true cause.</summary>
public sealed record SeededScore(int Labeled, int Correct);

/// <summary>Field bundles. Only labels of strength confirmed-by-fix or owner-reported, with a known cause, count toward accuracy.</summary>
/// <param name="ByStrength">Accuracy per counted label strength.</param>
/// <param name="GatePrecision">Both counted strengths together: the figure Gate 1 checks (at least 80%).</param>
/// <param name="WrongConfirmed">Confirmed diagnoses whose recipe is not the labeled cause (Gate 1 allows 1).</param>
/// <param name="Groups">All crash groups in field bundles.</param>
/// <param name="UnclearGroups">Of those, how many were diagnosed Unclear.</param>
/// <param name="Crashes">All crashes in field bundles.</param>
/// <param name="UnclearCrashes">Crashes in groups diagnosed Unclear (Gate 1 allows 40%).</param>
/// <param name="QualifyingBundles">Field bundles with at least one counted label (Gate 1 needs 10).</param>
public sealed record FieldScore(
    IReadOnlyDictionary<string, StrengthScore> ByStrength,
    StrengthScore GatePrecision,
    int WrongConfirmed,
    int Groups,
    int UnclearGroups,
    int Crashes,
    int UnclearCrashes,
    int QualifyingBundles);

/// <summary>Confident = Confirmed or Likely. Precision is ConfidentCorrect / ConfidentDiagnoses.</summary>
public sealed record StrengthScore(int Labeled, int ConfidentDiagnoses, int ConfidentCorrect);

/// <param name="CrashGroup">The group id, or "(no crash found)" when the label matched no crash in the bundle.</param>
/// <param name="Got">The matched recipe id, or null when none matched.</param>
public sealed record EvalMiss(string Bundle, string CrashGroup, string Expected, string? Got, ConfidenceLevel Confidence);
