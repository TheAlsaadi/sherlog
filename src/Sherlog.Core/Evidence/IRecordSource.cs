namespace Sherlog.Core.Evidence;

/// <summary>
/// Supplies raw crash records, either from this PC or from an evidence bundle (ADR 0003).
/// A source that cannot be read adds a plain-language warning instead of throwing.
/// </summary>
public interface IRecordSource
{
    IEnumerable<RawEventRecord> ReadEvents(DateTimeOffset since);

    IEnumerable<RawReliabilityRecord> ReadReliability(DateTimeOffset since);

    IReadOnlyList<string> Warnings { get; }
}
