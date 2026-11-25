using System.Text.RegularExpressions;
using Sherlog.Core.Evidence;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Diagnosis;

/// <summary>Checks one recipe condition against one crash (docs/recipe-format.md, "Conditions").</summary>
public static class ConditionMatcher
{
    public static bool Matches(MatchCondition condition, CrashEvent crash) =>
        SourceMatches(condition.Source, crash)
        && (condition.Provider is null || Same(condition.Provider, crash.Provider))
        && (condition.EventId is null || condition.EventId == crash.EventId)
        && (condition.AppName is null || Glob(condition.AppName, crash.AppName))
        && (condition.FaultingModule is null || Glob(condition.FaultingModule, crash.FaultingModule))
        && (condition.ExceptionCode is null || Same(condition.ExceptionCode, crash.ExceptionCode))
        && (condition.BugCheckCode is null || Same(condition.BugCheckCode, crash.BugCheckCode))
        && (condition.EventName is null || Same(condition.EventName, crash.EventName))
        && (condition.MessageContains is null
            || (crash.Message?.Contains(condition.MessageContains, StringComparison.OrdinalIgnoreCase) ?? false));

    // A Reliability Monitor row stands in for the event-log record of the same kind (the log may have rolled over).
    private static bool SourceMatches(string conditionSource, CrashEvent crash) =>
        conditionSource == crash.Source
        || (crash.Source == EvidenceSources.Reliability && CrashKind.OfSource(conditionSource) == CrashKind.Of(crash));

    private static bool Same(string expected, string? actual) =>
        string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static bool Glob(string pattern, string? value)
    {
        if (value is null)
        {
            return false;
        }

        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return Same(pattern, value);
        }

        var regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
