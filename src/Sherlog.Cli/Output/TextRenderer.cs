using System.Globalization;
using Sherlog.Core.Diagnosis;

namespace Sherlog.Cli.Output;

/// <summary>Plain-text output for people. Final wording comes from the Phase 1 wording pass.</summary>
internal static class TextRenderer
{
    public const string NoChangesLine = "Sherlog doesn't change anything on your PC in this version.";

    public static string NoCrashes(int days) => $"No crashes found in the last {days} days.";

    public static string Meaning(ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.Confirmed => "Confirmed means it has happened often enough, in the same way, that we're sure of the cause.",
        ConfidenceLevel.Likely => "Likely means a strong pattern points to this cause. The suggestion below is worth trying.",
        _ => "Unclear means we can't name the cause yet. Here is what we know and what you can try.",
    };

    public static void Crashes(TextWriter w, IReadOnlyList<Diagnosis> diagnoses, int days, TimeZoneInfo tz)
    {
        if (diagnoses.Count == 0)
        {
            w.WriteLine(NoCrashes(days));
            return;
        }

        var problems = diagnoses.Count == 1 ? "1 problem" : $"{diagnoses.Count} problems";
        w.WriteLine($"Crashes in the last {days} days: {problems}");
        w.WriteLine();
        foreach (var d in diagnoses)
        {
            var count = d.Group.Crashes.Count == 1 ? "once" : $"{d.Group.Crashes.Count} times";
            var last = TimeZoneInfo.ConvertTime(d.Group.Latest.Time, tz).ToString("MMM d 'at' HH:mm", CultureInfo.InvariantCulture);
            w.WriteLine($"{d.Confidence,-10} {d.Summary}");
            w.WriteLine($"{"",-10} Happened {count}, last on {last}. Id: {d.Group.Id}");
            w.WriteLine();
        }

        w.WriteLine("For details and what to try, run: sherlog explain <id>");
    }

    public static void Explain(TextWriter w, Diagnosis d)
    {
        w.WriteLine($"{d.Confidence}: {d.Summary}");
        w.WriteLine(Meaning(d.Confidence));

        if (d.Details is not null)
        {
            w.WriteLine();
            w.WriteLine("Why this happens");
            w.WriteLine($"  {d.Details}");
        }

        w.WriteLine();
        w.WriteLine("What Windows recorded");
        w.WriteLine($"  {d.WhatWeKnow}");

        if (d.Fixes.Count > 0)
        {
            w.WriteLine();
            w.WriteLine("What you can try");
            for (var i = 0; i < d.Fixes.Count; i++)
            {
                w.WriteLine($"  {i + 1}. {d.Fixes[i].Explain} ({d.Fixes[i].Risk} risk)");
            }
        }

        w.WriteLine();
        w.WriteLine(NoChangesLine);
    }
}
