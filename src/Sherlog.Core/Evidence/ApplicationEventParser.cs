namespace Sherlog.Core.Evidence;

/// <summary>Parses crash records from the Application log: events 1000, 1002 and 1001.</summary>
public static class ApplicationEventParser
{
    // Windows Error Reporting events worth keeping. Plain app crashes and hangs (APPCRASH, AppHang*, BEX*)
    // are already recorded by events 1000 and 1002; most other report types are not crashes.
    private static readonly HashSet<string> KeptReportNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LiveKernelEvent",
        "BlueScreen",
    };

    /// <summary>Returns the crash this record describes, or null when it is not an application crash record.</summary>
    public static CrashEvent? Parse(RawEventRecord record)
    {
        var xml = EventXml.TryParse(record.Xml);
        if (xml is null)
        {
            return null;
        }

        return (xml.Provider, xml.EventId) switch
        {
            ("Application Error", 1000) => Crash(xml, record, EvidenceSources.ApplicationError) with
            {
                AppName = CrashEvent.FileNameOnly(xml.Data("AppName", 0)),
                AppVersion = xml.Data("AppVersion", 1),
                FaultingModule = CrashEvent.FileNameOnly(xml.Data("ModuleName", 3)),
                ModuleVersion = xml.Data("ModuleVersion", 4),
                ExceptionCode = CrashEvent.NormalizeCode(xml.Data("ExceptionCode", 6)),
            },
            ("Application Hang", 1002) => Crash(xml, record, EvidenceSources.ApplicationHang) with
            {
                AppName = CrashEvent.FileNameOnly(xml.Data("AppName", 0)),
                AppVersion = xml.Data("AppVersion", 1),
            },
            ("Windows Error Reporting", 1001) => ParseErrorReport(xml, record),
            _ => null,
        };
    }

    private static CrashEvent? ParseErrorReport(EventXml xml, RawEventRecord record)
    {
        var eventName = xml.Data("EventName", 2);
        if (eventName is null || !KeptReportNames.Contains(eventName))
        {
            return null;
        }

        var code = string.Equals(eventName, "LiveKernelEvent", StringComparison.OrdinalIgnoreCase)
            ? CrashEvent.NormalizeCode(xml.Data("P1", 5))
            : null;

        return Crash(xml, record, EvidenceSources.ErrorReport) with { EventName = eventName, BugCheckCode = code };
    }

    private static CrashEvent Crash(EventXml xml, RawEventRecord record, string source) =>
        new(
            Id: "app-" + xml.RecordId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Time: xml.TimeUtc,
            Source: source,
            Provider: xml.Provider,
            EventId: xml.EventId,
            AppName: null,
            AppVersion: null,
            FaultingModule: null,
            ModuleVersion: null,
            ExceptionCode: null,
            BugCheckCode: null,
            EventName: null,
            Message: record.Message);
}
