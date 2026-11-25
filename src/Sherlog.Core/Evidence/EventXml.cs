using System.Globalization;
using System.Xml.Linq;

namespace Sherlog.Core.Evidence;

/// <summary>Reads the parts of a Windows event XML document that Sherlog's parsers need.</summary>
internal sealed class EventXml
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/win/2004/08/events/event";

    private readonly XElement[] _data;

    private EventXml(string provider, int eventId, long recordId, DateTimeOffset timeUtc, XElement[] data)
    {
        Provider = provider;
        EventId = eventId;
        RecordId = recordId;
        TimeUtc = timeUtc;
        _data = data;
    }

    public string Provider { get; }

    public int EventId { get; }

    public long RecordId { get; }

    public DateTimeOffset TimeUtc { get; }

    /// <summary>Parses the XML; returns null when it is not a usable event document.</summary>
    public static EventXml? TryParse(string xml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var system = doc.Root?.Element(Ns + "System");
        var provider = system?.Element(Ns + "Provider")?.Attribute("Name")?.Value;
        var idText = system?.Element(Ns + "EventID")?.Value;
        var recordText = system?.Element(Ns + "EventRecordID")?.Value;
        var timeText = system?.Element(Ns + "TimeCreated")?.Attribute("SystemTime")?.Value;

        if (provider is null
            || !int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var eventId)
            || !long.TryParse(recordText, NumberStyles.None, CultureInfo.InvariantCulture, out var recordId)
            || !DateTimeOffset.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
        {
            return null;
        }

        var data = doc.Root!.Element(Ns + "EventData")?.Elements(Ns + "Data").ToArray() ?? [];
        return new EventXml(provider, eventId, recordId, time.ToUniversalTime(), data);
    }

    /// <summary>
    /// Returns a data value by its Name attribute, falling back to its position when the event's data is unnamed.
    /// Empty values come back as null.
    /// </summary>
    public string? Data(string name, int position)
    {
        var named = _data.FirstOrDefault(d => string.Equals((string?)d.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase));
        string? value;
        if (named is not null)
        {
            value = named.Value;
        }
        else if (_data.All(d => d.Attribute("Name") is null) && position >= 0 && position < _data.Length)
        {
            value = _data[position].Value;
        }
        else
        {
            value = null;
        }

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
