using System.Globalization;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;

namespace Sherlog.Core.Diagnosis;

/// <summary>Writes the plain facts of a crash group: what failed, where, and how often.</summary>
internal static class CrashDescriber
{
    public static string Describe(CrashGroup group, TimeZoneInfo timeZone)
    {
        var latest = group.Latest;
        var when = When(group, timeZone);
        var app = latest.AppName ?? "An app";

        return CrashKind.Of(latest) switch
        {
            CrashKind.App => $"{app} closed unexpectedly {when}." + Where(latest),
            CrashKind.Hang => $"{app} stopped responding {when}.",
            CrashKind.Display => $"Your graphics driver ({latest.FaultingModule ?? "unknown"}) stopped responding and Windows restarted it {when}.",
            CrashKind.BugCheck => $"Windows stopped with a blue screen {when}" + Code(latest.BugCheckCode, "stop code") + ".",
            CrashKind.Shutdown => latest.BugCheckCode is null or "0x00000000"
                ? $"Your PC shut down unexpectedly {when}, without a blue screen. This often means it lost power or was switched off."
                : $"Your PC restarted after a blue screen {when}" + Code(latest.BugCheckCode, "stop code") + ".",
            CrashKind.Popup => $"{app} couldn't start because {latest.FaultingModule} is missing ({when.Replace("once on", "on", StringComparison.Ordinal)}).",
            CrashKind.Report => $"Windows recorded a problem with a driver or hardware {when}"
                + Code(latest.BugCheckCode, latest.EventName ?? "code") + ".",
            _ => $"Windows recorded a failure {when}.",
        };
    }

    private static string When(CrashGroup group, TimeZoneInfo timeZone)
    {
        var count = group.Crashes.Count;
        var first = TimeZoneInfo.ConvertTime(group.Crashes[^1].Time, timeZone);
        var date = first.ToString("MMM d", CultureInfo.InvariantCulture);
        return count == 1 ? $"once on {date}" : $"{count} times since {date}";
    }

    private static string Where(CrashEvent c)
    {
        if (c.FaultingModule is null && c.ExceptionCode is null)
        {
            return string.Empty;
        }

        var module = c.FaultingModule is null ? "Windows recorded the failure" : $"Windows recorded the failure in {c.FaultingModule}";
        return $" {module}{Code(c.ExceptionCode, "error")}.";
    }

    private static string Code(string? code, string label) => code is null ? string.Empty : $" ({label} {code})";
}
