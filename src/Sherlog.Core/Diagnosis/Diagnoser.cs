using System.Globalization;
using Sherlog.Core.Evidence;
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

    /// <summary>
    /// Diagnoses all of one PC's crash groups. A pattern recipe (ADR 0013) that holds across the PC's blue screens
    /// explains every blue-screen group in its window and wins over single-group recipes; the rest go to <see cref="Diagnose"/>.
    /// </summary>
    public IReadOnlyList<Diagnosis> DiagnoseAll(IEnumerable<CrashGroup> groups)
    {
        var list = groups.ToList();
        var byPattern = PatternMatches(list);
        return list.Select(g => byPattern.TryGetValue(g, out var match) ? FromPattern(g, match) : Diagnose(g)).ToList();
    }

    /// <summary>Diagnoses one crash group on its own. Pattern recipes need the whole PC, so they never apply here.</summary>
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

    private Diagnosis FromPattern(CrashGroup group, PatternMatch match)
    {
        var recipe = match.Recipe;
        var end = TimeZoneInfo.ConvertTime(match.End, _timeZone).ToString("MMM d", CultureInfo.InvariantCulture);
        var whatWeKnow = CrashDescriber.Describe(group, _timeZone)
                         + $" This PC had {match.BlueScreens} blue screens with {match.Codes} different stop codes"
                         + $" in the {match.Days} {(match.Days == 1 ? "day" : "days")} up to {end}.";

        // A pattern already uses the repeats as its evidence, so it is never raised to Confirmed (ADR 0013).
        return new Diagnosis(group, recipe, recipe.Confidence.Default, recipe.Summary, recipe.Details, recipe.Fixes, whatWeKnow);
    }

    // For each pattern recipe, lowest priority then id first: the window ends at the PC's latest blue screen, and when
    // it holds, every blue-screen group with a crash in the window is explained by it (unless an earlier pattern took it).
    private Dictionary<CrashGroup, PatternMatch> PatternMatches(IReadOnlyList<CrashGroup> groups)
    {
        var result = new Dictionary<CrashGroup, PatternMatch>(ReferenceEqualityComparer.Instance);
        var blueScreens = groups.SelectMany(g => g.Crashes.Where(IsBlueScreen).Select(c => (Group: g, Crash: c))).ToList();
        if (blueScreens.Count == 0)
        {
            return result;
        }

        var latest = blueScreens.Max(b => b.Crash.Time);
        var patterns = recipes
            .Where(r => r.Pattern is not null)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Id, StringComparer.Ordinal);
        foreach (var recipe in patterns)
        {
            var days = recipe.Pattern!.Days;
            var windowStart = latest - TimeSpan.FromDays(days);
            var inWindow = blueScreens.Where(b => b.Crash.Time >= windowStart).ToList();
            var codes = inWindow.Select(b => b.Crash.BugCheckCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (codes < recipe.Pattern.Count)
            {
                continue;
            }

            var match = new PatternMatch(recipe, inWindow.Count, codes, latest, days);
            foreach (var (group, _) in inWindow)
            {
                result.TryAdd(group, match);
            }
        }

        return result;
    }

    // Blue screens are BugCheck records (or Reliability Monitor rows for them) and Kernel-Power 41 with a stop code.
    // Live-kernel reports aren't blue screens, and Kernel-Power 41 with code 0 is a power loss (ADR 0013).
    private static bool IsBlueScreen(CrashEvent c) =>
        CrashKind.Of(c) is CrashKind.BugCheck or CrashKind.Shutdown
        && c.BugCheckCode is { } code
        && code != "0x00000000";

    private sealed record PatternMatch(Recipe Recipe, int BlueScreens, int Codes, DateTimeOffset End, int Days);

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
