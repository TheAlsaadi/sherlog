using System.Security.Cryptography;
using System.Text;
using Sherlog.Core.Evidence;

namespace Sherlog.Core.Grouping;

/// <summary>Groups crashes by signature so repeats of the same problem are counted together.</summary>
public static class CrashGrouper
{
    public static IReadOnlyList<CrashGroup> Group(IEnumerable<CrashEvent> crashes) =>
        crashes
            .GroupBy(Signature, StringComparer.Ordinal)
            .Select(g =>
            {
                var ordered = g.OrderByDescending(c => c.Time).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
                return new CrashGroup(IdFor(g.Key), g.Key, ordered);
            })
            .OrderByDescending(g => g.Latest.Time)
            .ThenBy(g => g.Id, StringComparer.Ordinal)
            .ToList();

    /// <summary>The lowercase signature that decides which group a crash belongs to.</summary>
    public static string Signature(CrashEvent c)
    {
        string[] parts = KindOf(c) switch
        {
            "app" => ["app", Part(c.AppName), Part(c.FaultingModule), Part(c.ExceptionCode)],
            "hang" => ["hang", Part(c.AppName)],
            "display" => ["display", Part(c.FaultingModule)],
            "bugcheck" => ["bugcheck", Part(c.BugCheckCode)],
            "shutdown" => ["shutdown", Part(c.BugCheckCode)],
            "wer" => ["wer", Part(c.EventName), Part(c.BugCheckCode)],
            var other => [other, Part(c.AppName), Part(c.FaultingModule), Part(c.ExceptionCode), Part(c.BugCheckCode)],
        };
        return string.Join('|', parts);
    }

    /// <summary>"g-" plus the first 6 hex characters of the signature's SHA-256.</summary>
    public static string IdFor(string signature)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(signature));
        return "g-" + Convert.ToHexStringLower(hash)[..6];
    }

    // Reliability rows describe the same kinds of crash as the logs, so they map onto the same kinds.
    private static string KindOf(CrashEvent c) => c.Source switch
    {
        EvidenceSources.ApplicationError => "app",
        EvidenceSources.ApplicationHang => "hang",
        EvidenceSources.DisplayReset => "display",
        EvidenceSources.BugCheck => "bugcheck",
        EvidenceSources.UnexpectedShutdown => "shutdown",
        EvidenceSources.ErrorReport => "wer",
        EvidenceSources.Reliability => c.EventId switch
        {
            1000 => "app",
            1002 => "hang",
            1001 => "bugcheck",
            _ => "reliability",
        },
        _ => c.Source,
    };

    private static string Part(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}
