using Sherlog.Core.Diagnosis;

namespace Sherlog.Core.Evaluation;

/// <summary>How well diagnoses match labels across a set of bundles (Gate 1, plan.md).</summary>
public sealed record EvalReport(
    SeededScore Seeded,
    FieldScore Field,
    IReadOnlyList<EvalMiss> Misses,
    int BundleCount,
    IReadOnlyList<string> UnreadableBundles);

/// <summary>Seeded crashes: <see cref="Correct"/> = the matched recipe is the true cause.</summary>
public sealed record SeededScore(int Labeled, int Correct);

/// <summary>Field bundles.</summary>
/// <param name="ByStrength">Scores per label strength, for labels with a known true cause.</param>
/// <param name="WrongConfirmed">Confirmed diagnoses whose recipe is not the labeled cause.</param>
/// <param name="Groups">All crash groups in field bundles (coverage).</param>
/// <param name="UnclearGroups">Of those, how many were diagnosed Unclear.</param>
/// <param name="QualifyingBundles">Field bundles with at least one confirmed-by-fix or owner-reported label of a known cause.</param>
public sealed record FieldScore(
    IReadOnlyDictionary<string, StrengthScore> ByStrength,
    int WrongConfirmed,
    int Groups,
    int UnclearGroups,
    int QualifyingBundles);

/// <summary>Confident = Confirmed or Likely. Precision is ConfidentCorrect / ConfidentDiagnoses.</summary>
public sealed record StrengthScore(int Labeled, int ConfidentDiagnoses, int ConfidentCorrect);

/// <param name="CrashGroup">The group id, or "(no crash found)" when the label matched no crash in the bundle.</param>
/// <param name="Got">The matched recipe id, or null when none matched.</param>
public sealed record EvalMiss(string Bundle, string CrashGroup, string Expected, string? Got, ConfidenceLevel Confidence);
