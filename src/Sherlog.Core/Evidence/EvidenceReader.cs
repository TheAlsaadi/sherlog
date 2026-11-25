namespace Sherlog.Core.Evidence;

/// <summary>Reads raw records from a source, parses them into crashes, removes duplicates and sorts newest first.</summary>
public sealed class EvidenceReader(IRecordSource source)
{
    private static readonly TimeSpan TwinWindow = TimeSpan.FromSeconds(60);

    // BugCheck 1001 is written after the dump is processed at the next boot, a few minutes from Kernel-Power 41.
    private static readonly TimeSpan BlueScreenTwinWindow = TimeSpan.FromMinutes(10);

    private readonly List<string> _warnings = [];

    /// <summary>Plain-language warnings about sources that could not be read.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    public IReadOnlyList<CrashEvent> ReadCrashes(DateTimeOffset since)
    {
        var fromLogs = source.ReadEvents(since)
            .Select(r => string.Equals(r.Log, "System", StringComparison.OrdinalIgnoreCase)
                ? SystemEventParser.Parse(r)
                : ApplicationEventParser.Parse(r))
            .OfType<CrashEvent>()
            .ToList();

        // Windows logs an error-report event each time it retries sending a queued report: count each report once,
        // at its earliest event, and only then apply the time window, so a recent retry of an old report isn't
        // mistaken for a recent crash.
        fromLogs = fromLogs
            .GroupBy(c => c.ReportId is null ? c.Id : "report:" + c.ReportId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(c => c.Time).First())
            .Where(c => c.Time >= since)
            .ToList();

        // A blue screen also leaves a Kernel-Power 41 event with the same bug-check code; count it once.
        fromLogs.RemoveAll(c => c.Source == EvidenceSources.UnexpectedShutdown && fromLogs.Any(b => IsBlueScreenTwin(b, c)));

        // Reliability Monitor repeats most event-log crashes; keep only the ones the logs don't already have.
        var fromReliability = source.ReadReliability(since)
            .Select(ReliabilityRecordParser.Parse)
            .OfType<CrashEvent>()
            .Where(c => c.Time >= since)
            .Where(r => !fromLogs.Any(e => IsTwin(e, r)))
            .ToList();

        // Copy warnings only after every record has been read: a lazy source adds them while it is enumerated.
        _warnings.Clear();
        _warnings.AddRange(source.Warnings);

        return fromLogs.Concat(fromReliability)
            .OrderByDescending(c => c.Time)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsBlueScreenTwin(CrashEvent bugCheck, CrashEvent shutdown) =>
        bugCheck.Source == EvidenceSources.BugCheck
        && shutdown.BugCheckCode is not null and not "0x00000000"
        && string.Equals(bugCheck.BugCheckCode, shutdown.BugCheckCode, StringComparison.OrdinalIgnoreCase)
        && (bugCheck.Time - shutdown.Time).Duration() <= BlueScreenTwinWindow;

    private static bool IsTwin(CrashEvent logged, CrashEvent reliability)
    {
        // Same kind of crash (Reliability keeps the original event ID) at nearly the same time.
        if (logged.EventId != reliability.EventId || (logged.Time - reliability.Time).Duration() > TwinWindow)
        {
            return false;
        }

        var sameApp = logged.AppName is not null
            && string.Equals(logged.AppName, reliability.AppName, StringComparison.OrdinalIgnoreCase);
        var sameBugCheck = logged.BugCheckCode is not null
            && string.Equals(logged.BugCheckCode, reliability.BugCheckCode, StringComparison.OrdinalIgnoreCase);
        return sameApp || sameBugCheck;
    }
}
