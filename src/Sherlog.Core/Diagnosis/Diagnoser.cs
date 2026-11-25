using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Diagnosis;

/// <summary>
/// Matches crash groups against recipes. The most specific matching recipe wins, then the lower priority,
/// then the recipe id. Confidence is the recipe's default, raised to Confirmed only by its repeat rules.
/// </summary>
/// <param name="timeZone">Used only to write dates in <see cref="Diagnosis.WhatWeKnow"/>. Defaults to this PC's time zone.</param>
public sealed class Diagnoser(IReadOnlyList<Recipe> recipes, TimeZoneInfo? timeZone = null)
{
    private readonly TimeZoneInfo _timeZone = timeZone ?? TimeZoneInfo.Local;

    public IReadOnlyList<Diagnosis> DiagnoseAll(IEnumerable<CrashGroup> groups) => groups.Select(Diagnose).ToList();

    public Diagnosis Diagnose(CrashGroup group)
    {
        var whatWeKnow = CrashDescriber.Describe(group, _timeZone);
        var best = recipes
            .Select(r => (Recipe: r, Specificity: Specificity(r, group)))
            .Where(x => x.Specificity >= 0)
            .OrderByDescending(x => x.Specificity)
            .ThenBy(x => x.Recipe.Priority)
            .ThenBy(x => x.Recipe.Id, StringComparer.Ordinal)
            .Select(x => x.Recipe)
            .FirstOrDefault();

        if (best is null)
        {
            var (summary, details) = UnclearExplainer.Explain(group);
            return new Diagnosis(group, null, ConfidenceLevel.Unclear, summary, details, [], whatWeKnow);
        }

        // Only a Likely match can be confirmed by repeats; an Unclear match stays Unclear however often it happens.
        var confidence = best.Confidence.Default == ConfidenceLevel.Likely
                         && best.Confidence.ConfirmedWhen.Any(rule => Holds(rule, group))
            ? ConfidenceLevel.Confirmed
            : best.Confidence.Default;
        return new Diagnosis(group, best, confidence, best.Summary, best.Details, best.Fixes, whatWeKnow);
    }

    /// <summary>The constrained-field count of the most specific condition that matches, or -1 when none does.</summary>
    private static int Specificity(Recipe recipe, CrashGroup group) =>
        recipe.Match
            .Where(c => ConditionMatcher.Matches(c, group.Latest))
            .Select(c => c.ConstrainedFieldCount)
            .DefaultIfEmpty(-1)
            .Max();

    // The window ends at the latest crash, never "now", so a bundle diagnosed later gets the same answer.
    private static bool Holds(RepeatRule rule, CrashGroup group)
    {
        var windowStart = group.Latest.Time - TimeSpan.FromDays(rule.Days);
        return group.Crashes.Count(c => c.Time >= windowStart) >= rule.Count;
    }
}
