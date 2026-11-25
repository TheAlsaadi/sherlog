using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sherlog.Core.Evidence;

namespace Sherlog.Core.Bundles;

/// <summary>
/// Writes an evidence bundle: redacted raw records, a system summary and the label file (ADRs 0003, 0005, 0006).
/// Every string is redacted before it is written.
/// </summary>
public static class BundleWriter
{
    public const int Format = 1;

    // Relaxed escaping keeps names in other scripts (for example Arabic) readable, so people can check the file.
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Write(string zipPath, IRecordSource source, DateTimeOffset since, SystemSummary system,
        LabelFile label, Redactor redactor, string sherlogVersion, DateTimeOffset createdUtc)
    {
        var events = source.ReadEvents(since)
            .Where(r => IsSince(r, since))
            .Select(r => new RawEventRecord(r.Log, redactor.Redact(r.Xml), r.Message is null ? null : redactor.Redact(r.Message)))
            .ToList();
        var reliability = source.ReadReliability(since)
            .Where(r => IsSince(r, since))
            .Select(r => new RawReliabilityRecord(
                r.Properties.ToDictionary(p => p.Key, p => p.Value is null ? null : redactor.Redact(p.Value))))
            .ToList();

        var manifest = new BundleManifest(Format, sherlogVersion, createdUtc, since, Redacted: true, events.Count, reliability.Count);
        var safeSystem = system with
        {
            Windows = redactor.Redact(system.Windows),
            Build = redactor.Redact(system.Build),
            Cpu = redactor.Redact(system.Cpu),
            Gpus = system.Gpus.Select(g => g with { Name = redactor.Redact(g.Name) }).ToList(),
        };

        // Written to a side file and moved into place, so a failure never leaves a half-written bundle behind.
        var tempPath = zipPath + ".partial";
        try
        {
            using (var zip = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                Entry(zip, "manifest.json", JsonSerializer.Serialize(manifest, Json));
                Entry(zip, "system.json", JsonSerializer.Serialize(safeSystem, Json));
                Entry(zip, "events.jsonl", Lines(events.Select(e => JsonSerializer.Serialize(e, Json))));
                Entry(zip, "reliability.jsonl", Lines(reliability.Select(r => JsonSerializer.Serialize(r.Properties, Json))));
                Entry(zip, "label.yaml", Redacted(label, redactor).ToYaml());
            }

            File.Move(tempPath, zipPath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    // Parse failures are kept: a record Sherlog can't read today may be readable after a parser fix (ADR 0003).
    private static bool IsSince(RawEventRecord record, DateTimeOffset since)
    {
        var crash = record.Log == "System" ? SystemEventParser.Parse(record) : ApplicationEventParser.Parse(record);
        return crash is null || crash.Time >= since;
    }

    private static bool IsSince(RawReliabilityRecord record, DateTimeOffset since)
    {
        var crash = ReliabilityRecordParser.Parse(record);
        return crash is null || crash.Time >= since;
    }

    // Redact the answers, not the written YAML: a profile named "Owner" would otherwise rename the "owner:" key.
    private static LabelFile Redacted(LabelFile label, Redactor r)
    {
        string? R(string? s) => s is null ? null : r.Redact(s);
        var o = label.Owner;
        return new LabelFile(
            new OwnerAnswers(R(o.WhatCrashed), R(o.Doing), o.RecentChanges.Select(r.Redact).ToList(), o.Tried.Select(r.Redact).ToList(),
                R(o.Helped), R(o.StillHappens), R(o.Note)),
            label.Investigator with
            {
                Labels = label.Investigator.Labels
                    .Select(l => l with { AppName = R(l.AppName), Evidence = R(l.Evidence) })
                    .ToList(),
            });
    }

    private static string Lines(IEnumerable<string> lines) => string.Concat(lines.Select(l => l + "\n"));

    private static void Entry(ZipArchive zip, string name, string content)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}

public sealed record BundleManifest(
    int Format,
    string SherlogVersion,
    DateTimeOffset CreatedUtc,
    DateTimeOffset SinceUtc,
    bool Redacted,
    int EventCount,
    int ReliabilityCount);
