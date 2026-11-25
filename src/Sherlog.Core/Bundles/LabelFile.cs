using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Sherlog.Core.Bundles;

/// <summary>
/// A bundle's <c>label.yaml</c> (ADR 0006): the PC owner's answers, and the investigator's labels, one per crash group.
/// </summary>
public sealed record LabelFile(OwnerAnswers Owner, InvestigatorSection Investigator)
{
    public static readonly IReadOnlySet<string> Strengths =
        new HashSet<string>(StringComparer.Ordinal) { "seeded", "confirmed-by-fix", "owner-reported", "unknown" };

    /// <summary>Parses a label file. Throws <see cref="InvalidDataException"/> when it can't be read or has invalid values.</summary>
    public static LabelFile Parse(string yaml)
    {
        Dto dto;
        try
        {
            dto = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build()
                .Deserialize<Dto?>(yaml) ?? new Dto();
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException($"label.yaml line {ex.Start.Line}: {ex.Message}", ex);
        }

        var o = dto.Owner ?? new OwnerDto();
        var owner = new OwnerAnswers(Blank(o.WhatCrashed), Blank(o.Doing), Clean(o.RecentChanges), Clean(o.Tried),
            Blank(o.Helped), Blank(o.StillHappens), Blank(o.Note));

        var labels = (dto.Investigator?.Labels ?? []).Select((l, i) =>
        {
            var cause = Blank(l.TrueCause) ?? throw new InvalidDataException($"label.yaml label {i + 1}: true_cause is empty (use 'unknown').");
            var strength = Blank(l.LabelStrength) ?? "unknown";
            if (!Strengths.Contains(strength))
            {
                throw new InvalidDataException($"label.yaml label {i + 1}: label_strength '{strength}' must be one of {string.Join(", ", Strengths)}.");
            }

            return new CrashLabel(Blank(l.CrashGroup), Blank(l.AppName), cause, Blank(l.Evidence), strength);
        }).ToList();

        return new LabelFile(owner, new InvestigatorSection(dto.Investigator?.Seeded ?? false, labels));
    }

    /// <summary>Writes the file with every question as a comment, so it can also be filled in by hand.</summary>
    public string ToYaml()
    {
        var y = new StringBuilder();
        y.AppendLine("# Sherlog evidence bundle label (format 1)");
        y.AppendLine("format: 1");
        y.AppendLine();
        y.AppendLine("# The PC owner's answers. If they were skipped, they can be filled in here by hand.");
        y.AppendLine("owner:");
        Line(y, $"1. {OwnerQuestions.WhatCrashedQuestion}", "what_crashed", Owner.WhatCrashed);
        Line(y, $"2. {OwnerQuestions.DoingQuestion} ({string.Join(" / ", OwnerQuestions.DoingChoices)})", "doing", Owner.Doing);
        List(y, $"3. {OwnerQuestions.ChangesQuestion} (any of: {string.Join(" / ", OwnerQuestions.ChangeChoices)})", "recent_changes", Owner.RecentChanges);
        List(y, $"4. {OwnerQuestions.TriedQuestion} (any of: {string.Join(" / ", OwnerQuestions.TriedChoices)})", "tried", Owner.Tried);
        Line(y, $"5. {OwnerQuestions.HelpedQuestion} ({string.Join(" / ", OwnerQuestions.HelpedChoices)})", "helped", Owner.Helped);
        Line(y, $"6. {OwnerQuestions.StillQuestion} ({string.Join(" / ", OwnerQuestions.StillChoices)})", "still_happens", Owner.StillHappens);
        Line(y, $"7. {OwnerQuestions.NoteQuestion}", "note", Owner.Note);
        y.AppendLine();
        y.AppendLine("# For the investigator: one entry per crash group in this bundle.");
        y.AppendLine("# true_cause: a recipe id, or unknown.");
        y.AppendLine("# label_strength: seeded, confirmed-by-fix, owner-reported or unknown.");
        y.AppendLine("investigator:");
        y.AppendLine($"  seeded: {(Investigator.Seeded ? "true" : "false")}");
        if (Investigator.Labels.Count == 0)
        {
            y.AppendLine("  labels: []");
        }
        else
        {
            y.AppendLine("  labels:");
            foreach (var l in Investigator.Labels)
            {
                y.AppendLine($"    - crash_group: {Quote(l.GroupId)}");
                y.AppendLine($"      app_name: {Quote(l.AppName)}");
                y.AppendLine($"      true_cause: {Quote(l.TrueCause)}");
                y.AppendLine($"      evidence: {Quote(l.Evidence)}");
                y.AppendLine($"      label_strength: {Quote(l.LabelStrength)}");
            }
        }

        return y.ToString();
    }

    private static void Line(StringBuilder y, string comment, string key, string? value)
    {
        y.AppendLine($"  # {comment}");
        y.AppendLine($"  {key}: {Quote(value)}");
    }

    private static void List(StringBuilder y, string comment, string key, IReadOnlyList<string> values)
    {
        y.AppendLine($"  # {comment}");
        y.AppendLine($"  {key}: [{string.Join(", ", values.Select(Quote))}]");
    }

    private static string Quote(string? value)
    {
        var s = new StringBuilder("\"");
        foreach (var ch in value ?? string.Empty)
        {
            s.Append(ch switch
            {
                '\\' => @"\\",
                '"' => "\\\"",
                '\n' => @"\n",
                '\r' => @"\r",
                '\t' => @"\t",
                < ' ' => $"\\x{(int)ch:x2}",
                _ => ch.ToString(),
            });
        }

        return s.Append('"').ToString();
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<string> Clean(List<string>? items) =>
        (items ?? []).Select(Blank).OfType<string>().ToList();

    private sealed class Dto
    {
        public int Format { get; set; } = 1;

        public OwnerDto? Owner { get; set; }

        public InvestigatorDto? Investigator { get; set; }
    }

    private sealed class OwnerDto
    {
        public string? WhatCrashed { get; set; }

        public string? Doing { get; set; }

        public List<string>? RecentChanges { get; set; }

        public List<string>? Tried { get; set; }

        public string? Helped { get; set; }

        public string? StillHappens { get; set; }

        public string? Note { get; set; }
    }

    private sealed class InvestigatorDto
    {
        public bool Seeded { get; set; }

        public List<LabelDto>? Labels { get; set; }
    }

    private sealed class LabelDto
    {
        public string? CrashGroup { get; set; }

        public string? AppName { get; set; }

        public string? TrueCause { get; set; }

        public string? Evidence { get; set; }

        public string? LabelStrength { get; set; }
    }
}

public sealed record OwnerAnswers(
    string? WhatCrashed,
    string? Doing,
    IReadOnlyList<string> RecentChanges,
    IReadOnlyList<string> Tried,
    string? Helped,
    string? StillHappens,
    string? Note)
{
    public static readonly OwnerAnswers Empty = new(null, null, [], [], null, null, null);

    public bool Equals(OwnerAnswers? other) =>
        other is not null
        && WhatCrashed == other.WhatCrashed && Doing == other.Doing && Helped == other.Helped
        && StillHappens == other.StillHappens && Note == other.Note
        && RecentChanges.SequenceEqual(other.RecentChanges) && Tried.SequenceEqual(other.Tried);

    public override int GetHashCode() => HashCode.Combine(WhatCrashed, Doing, Helped, StillHappens, Note);
}

public sealed record InvestigatorSection(bool Seeded, IReadOnlyList<CrashLabel> Labels);

/// <summary>An investigator's conclusion about one crash group (identified by its id, or by app name).</summary>
/// <param name="TrueCause">A recipe id, or "unknown".</param>
/// <param name="LabelStrength">seeded, confirmed-by-fix, owner-reported or unknown.</param>
public sealed record CrashLabel(string? GroupId, string? AppName, string TrueCause, string? Evidence, string LabelStrength);
