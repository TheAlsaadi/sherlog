using System.Globalization;

namespace Sherlog.Core.Evidence;

/// <summary>Parses Reliability Monitor rows (WMI <c>Win32_ReliabilityRecords</c>) into crashes.</summary>
public static class ReliabilityRecordParser
{
    /// <summary>Separates the items of the WMI <c>InsertionStrings</c> array when it is stored as one string.</summary>
    public const char InsertionSeparator = '\u001f';

    /// <summary>Returns the crash this row describes, or null when it is not a crash record.</summary>
    public static CrashEvent? Parse(RawReliabilityRecord record)
    {
        var p = record.Properties;
        var sourceName = Get(p, "SourceName");
        var eventId = ParseEventId(Get(p, "EventIdentifier"));
        var time = ParseWmiTime(Get(p, "TimeGenerated"));
        var recordNumber = Get(p, "RecordNumber");
        if (sourceName is null || eventId is null || time is null || recordNumber is null)
        {
            return null;
        }

        var inserts = Get(p, "InsertionStrings")?.Split(InsertionSeparator) ?? [];
        string? Insert(int i) => i < inserts.Length && !string.IsNullOrWhiteSpace(inserts[i]) ? inserts[i].Trim() : null;

        var crash = new CrashEvent(
            Id: "rel-" + recordNumber,
            Time: time.Value,
            Source: EvidenceSources.Reliability,
            Provider: sourceName,
            EventId: eventId,
            AppName: null,
            AppVersion: null,
            FaultingModule: null,
            ModuleVersion: null,
            ExceptionCode: null,
            BugCheckCode: null,
            EventName: null,
            Message: Get(p, "Message"));

        return (sourceName, eventId) switch
        {
            ("Application Error", 1000) => crash with
            {
                AppName = CrashEvent.FileNameOnly(Insert(0) ?? Get(p, "ProductName")),
                AppVersion = Insert(1),
                FaultingModule = CrashEvent.FileNameOnly(Insert(3)),
                ModuleVersion = Insert(4),
                ExceptionCode = CrashEvent.NormalizeCode(Insert(6)),
            },
            ("Application Hang", 1002) => crash with
            {
                AppName = CrashEvent.FileNameOnly(Insert(0) ?? Get(p, "ProductName")),
                AppVersion = Insert(1),
            },
            ("Microsoft-Windows-WER-SystemErrorReporting", 1001) or ("BugCheck", 1001) => crash with
            {
                BugCheckCode = SystemEventParser.FirstCode(Insert(0)),
            },
            _ => null,
        };
    }

    private static string? Get(IReadOnlyDictionary<string, string?> p, string key) =>
        p.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    // WMI event identifiers for classic events can carry qualifier bits above the low 16.
    private static int? ParseEventId(string? text) =>
        uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? (int)(id & 0xFFFF) : null;

    /// <summary>Parses a WMI datetime such as "20261120143000.000000-000" (offset in minutes) to UTC.</summary>
    internal static DateTimeOffset? ParseWmiTime(string? text)
    {
        if (text is null || text.Length < 25)
        {
            return null;
        }

        if (!DateTime.TryParseExact(text[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            || !int.TryParse(text[15..21], NumberStyles.None, CultureInfo.InvariantCulture, out var micros)
            || !int.TryParse(text[22..25], NumberStyles.None, CultureInfo.InvariantCulture, out var offsetMinutes))
        {
            return null;
        }

        var sign = text[21] == '-' ? -1 : 1;
        var offset = TimeSpan.FromMinutes(sign * offsetMinutes);
        return new DateTimeOffset(local.AddTicks(micros * 10L), offset).ToUniversalTime();
    }
}
