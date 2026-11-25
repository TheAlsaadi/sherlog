using System.IO.Compression;
using System.Text.Json;
using Sherlog.Core.Evidence;
using IOPath = System.IO.Path;

namespace Sherlog.Core.Bundles;

/// <summary>Reads an evidence bundle written by <see cref="BundleWriter"/>. Everything is loaded at once; bundles are small.</summary>
public sealed class BundleReader
{
    private BundleReader(string path, BundleManifest manifest, SystemSummary system, LabelFile label, BundleRecordSource records)
    {
        Path = path;
        Manifest = manifest;
        System = system;
        Label = label;
        Records = records;
    }

    public string Path { get; }

    public BundleManifest Manifest { get; }

    public SystemSummary System { get; }

    public LabelFile Label { get; }

    public BundleRecordSource Records { get; }

    /// <summary>Opens a bundle. Throws <see cref="InvalidDataException"/> when the file isn't a readable bundle.</summary>
    public static BundleReader Open(string zipPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var manifest = Json<BundleManifest>(zip, "manifest.json");
            if (manifest.Format != BundleWriter.Format)
            {
                throw new InvalidDataException($"This bundle uses format {manifest.Format}; this Sherlog reads format {BundleWriter.Format}.");
            }

            var events = JsonLines<RawEventRecord>(zip, "events.jsonl");
            var reliability = JsonLines<Dictionary<string, string?>>(zip, "reliability.jsonl")
                .Select(p => new RawReliabilityRecord(p))
                .ToList();
            var label = zip.GetEntry("label.yaml") is { } entry
                ? LabelFile.Parse(Text(entry))
                : new LabelFile(OwnerAnswers.Empty, new InvestigatorSection(false, []));

            return new BundleReader(zipPath, manifest, Json<SystemSummary>(zip, "system.json"), label,
                new BundleRecordSource(events, reliability, manifest.CreatedUtc));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException($"{IOPath.GetFileName(zipPath)} is not a readable Sherlog evidence bundle.", ex);
        }
    }

    private static T Json<T>(ZipArchive zip, string name) =>
        JsonSerializer.Deserialize<T>(Text(Required(zip, name)), BundleWriter.Json)
        ?? throw new InvalidDataException($"{name} is empty.");

    private static List<T> JsonLines<T>(ZipArchive zip, string name) =>
        Text(Required(zip, name))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<T>(line, BundleWriter.Json) ?? throw new InvalidDataException($"{name} has an empty line."))
            .ToList();

    private static ZipArchiveEntry Required(ZipArchive zip, string name) =>
        zip.GetEntry(name) ?? throw new InvalidDataException($"The bundle has no {name}.");

    private static string Text(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }
}

/// <summary>Records from a bundle. <see cref="CreatedUtc"/> stands in for "now" when looking back a number of days.</summary>
public sealed class BundleRecordSource(IReadOnlyList<RawEventRecord> events, IReadOnlyList<RawReliabilityRecord> reliability,
    DateTimeOffset createdUtc) : IRecordSource
{
    public DateTimeOffset CreatedUtc { get; } = createdUtc;

    public IReadOnlyList<string> Warnings => [];

    public IEnumerable<RawEventRecord> ReadEvents(DateTimeOffset since) => events;

    public IEnumerable<RawReliabilityRecord> ReadReliability(DateTimeOffset since) => reliability;
}
