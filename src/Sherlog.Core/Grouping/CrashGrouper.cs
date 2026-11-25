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
        string[] parts = CrashKind.Of(c) switch
        {
            CrashKind.App => ["app", Part(c.AppName), Part(c.FaultingModule), Part(c.ExceptionCode)],
            CrashKind.Hang => ["hang", Part(c.AppName)],
            CrashKind.Display => ["display", Part(c.FaultingModule)],
            CrashKind.BugCheck => ["bugcheck", Part(c.BugCheckCode)],
            CrashKind.Shutdown => ["shutdown", Part(c.BugCheckCode)],
            CrashKind.Report => ["wer", Part(c.EventName), Part(c.BugCheckCode)],
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

    private static string Part(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}
