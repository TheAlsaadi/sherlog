using System.Globalization;

namespace Sherlog.Core.Evidence;

/// <summary>Parses crash records from the System log: display driver resets, blue screens and unexpected shutdowns.</summary>
public static class SystemEventParser
{
    /// <summary>Returns the crash this record describes, or null when it is not a system crash record.</summary>
    public static CrashEvent? Parse(RawEventRecord record)
    {
        var xml = EventXml.TryParse(record.Xml);
        if (xml is null)
        {
            return null;
        }

        return (xml.Provider, xml.EventId) switch
        {
            ("Display", 4101) => Crash(xml, record, EvidenceSources.DisplayReset) with
            {
                FaultingModule = xml.Data("Driver", 0),
            },
            ("Microsoft-Windows-WER-SystemErrorReporting", 1001) => Crash(xml, record, EvidenceSources.BugCheck) with
            {
                BugCheckCode = FirstCode(xml.Data("param1", 0)),
            },
            ("Microsoft-Windows-Kernel-Power", 41) => Crash(xml, record, EvidenceSources.UnexpectedShutdown) with
            {
                BugCheckCode = CrashEvent.NormalizeCode(xml.Data("BugcheckCode", 0), fromDecimal: true),
            },
            _ => null,
        };
    }

    /// <summary>Takes the leading code from text such as "0x00000116 (0xffff..., ...)".</summary>
    internal static string? FirstCode(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var end = text.IndexOfAny([' ', '(']);
        return CrashEvent.NormalizeCode(end > 0 ? text[..end] : text);
    }

    private static CrashEvent Crash(EventXml xml, RawEventRecord record, string source) =>
        new(
            Id: "sys-" + xml.RecordId.ToString(CultureInfo.InvariantCulture),
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
