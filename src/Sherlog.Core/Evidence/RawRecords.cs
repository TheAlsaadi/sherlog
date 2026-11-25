namespace Sherlog.Core.Evidence;

/// <summary>An event-log record as Windows stores it: its XML, plus the rendered message when one was available.</summary>
/// <param name="Log">"Application" or "System".</param>
public sealed record RawEventRecord(string Log, string Xml, string? Message);

/// <summary>One Reliability Monitor row, as WMI property names and values.</summary>
public sealed record RawReliabilityRecord(IReadOnlyDictionary<string, string?> Properties);
