using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Sherlog.Core.Evidence;
using Sherlog.Core.Recipes;
using YamlDotNet.Core;

namespace Sherlog.Core.Evaluation;

/// <summary>
/// A solved public tech-support thread turned into a test case (ADR 0010): the crash facts the post gives, and the
/// fix the poster said worked as the label. Format: <c>tests/public-cases/README.md</c>.
/// </summary>
public sealed record PublicCase(
    string Id,
    string SourceUrl,
    IReadOnlyList<CaseCrash> Crashes,
    string TrueCause,
    string LabelStrength,
    string? Evidence)
{
    /// <summary>Turns the crash entries into crash events, each entry's crashes spread evenly over its days, the last at <paramref name="end"/>.</summary>
    public IReadOnlyList<CrashEvent> ToCrashes(DateTimeOffset end)
    {
        var events = new List<CrashEvent>();
        for (var e = 0; e < Crashes.Count; e++)
        {
            var c = Crashes[e];
            var span = TimeSpan.FromDays(c.Days - 1);
            for (var i = 0; i < c.Count; i++)
            {
                var back = c.Count == 1 ? TimeSpan.Zero : span * (c.Count - 1 - i) / (c.Count - 1);
                events.Add(new CrashEvent(
                    Id: string.Create(CultureInfo.InvariantCulture, $"{Id}-{e + 1}-{i + 1}"),
                    Time: end - back,
                    Source: c.Source,
                    Provider: Provider(c.Source),
                    EventId: EventId(c.Source),
                    AppName: c.AppName,
                    AppVersion: null,
                    FaultingModule: c.FaultingModule,
                    ModuleVersion: null,
                    ExceptionCode: c.ExceptionCode,
                    BugCheckCode: c.BugCheckCode,
                    EventName: c.EventName,
                    Message: null));
            }
        }

        return events;
    }

    // The provider and event id the readers give each source, so recipes see the same thing as on a real PC.
    private static string? Provider(string source) => source switch
    {
        EvidenceSources.ApplicationError => "Application Error",
        EvidenceSources.ApplicationHang => "Application Hang",
        EvidenceSources.ErrorReport => "Windows Error Reporting",
        EvidenceSources.DisplayReset => "Display",
        EvidenceSources.BugCheck => "Microsoft-Windows-WER-SystemErrorReporting",
        EvidenceSources.UnexpectedShutdown => "Microsoft-Windows-Kernel-Power",
        EvidenceSources.AppPopup => "Application Popup",
        _ => null,
    };

    private static int? EventId(string source) => source switch
    {
        EvidenceSources.ApplicationError => 1000,
        EvidenceSources.ApplicationHang => 1002,
        EvidenceSources.ErrorReport or EvidenceSources.BugCheck => 1001,
        EvidenceSources.DisplayReset => 4101,
        EvidenceSources.UnexpectedShutdown => 41,
        EvidenceSources.AppPopup => 26,
        _ => null,
    };
}

/// <summary>One kind of crash in a public case, repeated <see cref="Count"/> times over <see cref="Days"/> days.</summary>
public sealed record CaseCrash(
    string Source,
    string? AppName,
    string? FaultingModule,
    string? ExceptionCode,
    string? BugCheckCode,
    string? EventName,
    int Count,
    int Days);

/// <summary>Reads and checks public case files. Any problem is an <see cref="InvalidDataException"/> naming the field.</summary>
public static partial class PublicCaseFile
{
    private static readonly HashSet<string> Strengths = new(StringComparer.Ordinal) { "confirmed-by-fix", "owner-reported" };

    // Reliability rows need Windows' own event ids to mean anything, so a case names the event-log source instead.
    private static readonly HashSet<string> Sources = new(
        EvidenceSources.All.Where(s => s != EvidenceSources.Reliability), StringComparer.Ordinal);

    public static PublicCase Load(string path)
    {
        JsonNode? root;
        try
        {
            root = YamlJson.Parse(File.ReadAllText(path));
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException($"YAML syntax error at line {ex.Start.Line}: {ex.Message}", ex);
        }

        if (root is not JsonObject doc)
        {
            throw new InvalidDataException("The case file is empty or not a map.");
        }

        if (Int(doc, "format", null) != 1)
        {
            throw new InvalidDataException("format: must be 1.");
        }

        var id = Required(doc, "id");
        if (!IdPattern().IsMatch(id))
        {
            throw new InvalidDataException("id: use lowercase letters, digits and dashes, for example pc-0001.");
        }

        var url = Required(doc, "source_url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("source_url: must be an https link to the thread.");
        }

        if (doc["crashes"] is not JsonArray list || list.Count == 0)
        {
            throw new InvalidDataException("crashes: list at least one crash.");
        }

        var crashes = list.Select((node, i) => Crash(node as JsonObject, i + 1)).ToList();

        if (doc["truth"] is not JsonObject truth)
        {
            throw new InvalidDataException("truth: missing.");
        }

        var cause = Required(truth, "true_cause");
        var strength = Required(truth, "label_strength");
        if (!Strengths.Contains(strength))
        {
            throw new InvalidDataException("label_strength: must be confirmed-by-fix or owner-reported.");
        }

        return new PublicCase(id, url, crashes, cause, strength, Optional(truth, "evidence"));
    }

    private static CaseCrash Crash(JsonObject? c, int n)
    {
        if (c is null)
        {
            throw new InvalidDataException($"crashes[{n}]: must be a map of crash fields.");
        }

        var source = Required(c, "source", $"crashes[{n}].");
        if (!Sources.Contains(source))
        {
            throw new InvalidDataException($"crashes[{n}].source: '{source}' isn't a source a case can use.");
        }

        var exception = Code(c, "exception_code", n);
        var bugcheck = Code(c, "bugcheck_code", n);
        var module = CrashEvent.FileNameOnly(Optional(c, "faulting_module"));
        var eventName = Optional(c, "event_name");
        if (module is null && exception is null && bugcheck is null && eventName is null
            && source is not (EvidenceSources.ApplicationHang or EvidenceSources.UnexpectedShutdown))
        {
            throw new InvalidDataException($"crashes[{n}]: give at least one recorded fact (faulting_module, exception_code, bugcheck_code or event_name).");
        }

        return new CaseCrash(
            source,
            CrashEvent.FileNameOnly(Optional(c, "app_name")),
            module,
            exception,
            bugcheck,
            eventName,
            Int(c, "count", 1, $"crashes[{n}].", 1, 100),
            Int(c, "days", 1, $"crashes[{n}].", 1, 365));
    }

    private static string? Code(JsonObject c, string key, int n)
    {
        var raw = Optional(c, key);
        if (raw is null)
        {
            return null;
        }

        return CrashEvent.NormalizeCode(raw)
               ?? throw new InvalidDataException($"crashes[{n}].{key}: '{raw}' isn't a hex code.");
    }

    private static string Required(JsonObject o, string key, string prefix = "") =>
        Optional(o, key) ?? throw new InvalidDataException($"{prefix}{key}: missing or empty.");

    private static string? Optional(JsonObject o, string key)
    {
        var text = o[key] is JsonValue v ? v.ToString().Trim() : null;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static int? Int(JsonObject o, string key, int? fallback, string prefix = "", int min = int.MinValue, int max = int.MaxValue)
    {
        var raw = Optional(o, key);
        if (raw is null)
        {
            return fallback;
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
        {
            throw new InvalidDataException($"{prefix}{key}: must be a whole number from {min} to {max}.");
        }

        return value;
    }

    private static int Int(JsonObject o, string key, int fallback, string prefix, int min, int max) =>
        Int(o, key, (int?)fallback, prefix, min, max)!.Value;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
