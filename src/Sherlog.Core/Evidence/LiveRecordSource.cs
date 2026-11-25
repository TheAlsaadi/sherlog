using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Management;
using System.Runtime.Versioning;

namespace Sherlog.Core.Evidence;

/// <summary>
/// Reads crash records from this PC: the Application and System event logs and Reliability Monitor.
/// Needs no admin rights. A log that can't be read adds a warning and the others are still read.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LiveRecordSource : IRecordSource
{
    private static readonly IReadOnlyDictionary<string, (string Provider, int EventId)[]> Filters =
        new Dictionary<string, (string, int)[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Application"] =
            [
                ("Application Error", 1000),
                ("Application Hang", 1002),
                ("Windows Error Reporting", 1001),
            ],
            ["System"] =
            [
                ("Display", 4101),
                ("Microsoft-Windows-WER-SystemErrorReporting", 1001),
                ("Microsoft-Windows-Kernel-Power", 41),
            ],
        };

    private static readonly string[] ReliabilityProperties =
        ["SourceName", "EventIdentifier", "ProductName", "RecordNumber", "TimeGenerated", "Message", "InsertionStrings"];

    private readonly List<string> _warnings = [];

    public IReadOnlyList<string> Warnings => _warnings;

    public IEnumerable<RawEventRecord> ReadEvents(DateTimeOffset since)
    {
        var records = new List<RawEventRecord>();
        foreach (var log in Filters.Keys)
        {
            try
            {
                ReadLog(log, since, records);
            }
            catch (Exception ex) when (ex is EventLogException or UnauthorizedAccessException)
            {
                _warnings.Add(log == "System"
                    ? "Couldn't read the System log, so blue screens and graphics driver resets may be missing."
                    : "Couldn't read the Application log, so app crashes may be missing.");
            }
        }

        return records;
    }

    public IEnumerable<RawReliabilityRecord> ReadReliability(DateTimeOffset since)
    {
        var records = new List<RawReliabilityRecord>();
        try
        {
            var wmiSince = ManagementDateTimeConverter.ToDmtfDateTime(since.UtcDateTime);
            var query = $"SELECT {string.Join(", ", ReliabilityProperties)} FROM Win32_ReliabilityRecords WHERE TimeGenerated >= '{wmiSince}'";
            using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
            using var results = searcher.Get();
            foreach (var row in results)
            {
                using (row)
                {
                    records.Add(new RawReliabilityRecord(ReadRow(row)));
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _warnings.Add("Couldn't read Reliability Monitor, so some older crashes may be missing.");
        }

        return records;
    }

    /// <summary>Builds the XPath query for one log: only the crash providers and events, from <paramref name="since"/> on.</summary>
    public static string BuildQuery(string log, DateTimeOffset since)
    {
        var providers = string.Join(" or ", Filters[log].Select(f => $"(Provider[@Name='{f.Provider}'] and EventID={f.EventId})"));
        var time = since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        return $"*[System[({providers}) and TimeCreated[@SystemTime>='{time}']]]";
    }

    private static void ReadLog(string log, DateTimeOffset since, List<RawEventRecord> into)
    {
        var query = new EventLogQuery(log, PathType.LogName, BuildQuery(log, since)) { ReverseDirection = true };
        using var reader = new EventLogReader(query);
        for (var record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
        {
            using (record)
            {
                into.Add(new RawEventRecord(log, record.ToXml(), TryFormat(record)));
            }
        }
    }

    private static string? TryFormat(EventRecord record)
    {
        try
        {
            return record.FormatDescription();
        }
        catch (EventLogException)
        {
            return null;
        }
    }

    private static Dictionary<string, string?> ReadRow(ManagementBaseObject row)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var name in ReliabilityProperties)
        {
            var value = row[name];
            values[name] = value switch
            {
                null => null,
                string[] items => string.Join(ReliabilityRecordParser.InsertionSeparator, items),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }

        return values;
    }
}
