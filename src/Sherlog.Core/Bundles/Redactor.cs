using System.Text.RegularExpressions;

namespace Sherlog.Core.Bundles;

/// <summary>
/// Replaces anything that identifies a person or PC with fixed placeholders (ADR 0005): the event's computer name and
/// this PC's names become <c>PC</c>, domains <c>DOMAIN</c>, user and profile-folder names <c>USER</c>, account SIDs
/// <c>S-1-5-REDACTED</c> and email addresses <c>user@example.invalid</c>.
/// </summary>
public sealed class Redactor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    // Every event names its computer here, as the full DNS name (not the 15-character NetBIOS name).
    private static readonly Regex ComputerElement = new(@"(?i)<Computer>[^<]*</Computer>", RegexOptions.CultureInvariant, Timeout);

    // A profile root, then the profile folder name. Roots: "C:\Users\", "\\host\c$\Users\", "\Device\HarddiskVolume3\Users\",
    // or "Documents and Settings"; separators may be \, \\ (JSON-escaped) or /. A name followed by a separator may contain
    // spaces; a bare profile path (end of a sentence) stops at the first space. U+001F separates Reliability fields.
    private static readonly Regex ProfilePath = new(
        @"(?i)((?:[a-z]:|\\\\[^\\/\s]+\\[a-z]\$|\\device\\harddiskvolume\d+)(?:\\{1,2}|/)(?:users|documents and settings)(?:\\{1,2}|/))"
        + @"(?:[^\\/""<>\r\n\u001f]{1,64}(?=[\\/])|[^\\/""<>\s\u001f]+)",
        RegexOptions.CultureInvariant,
        Timeout);

    // Local and domain account SIDs (S-1-5-21-…) and Microsoft Entra ID SIDs (S-1-12-1-…). Well-known SIDs stay.
    private static readonly Regex AccountSid = new(@"S-1-5-21-\d+(?:-\d+){2,4}|S-1-12-1-\d+(?:-\d+){3}", RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex Email = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant, Timeout);

    private readonly List<(Regex Pattern, string Replacement)> _names = [];

    public Redactor(RedactionTargets targets)
    {
        var domains = (targets.DnsDomains ?? []).Append(targets.DomainName);
        var hosts = (targets.HostNames ?? []).Append(targets.MachineName);
        var users = targets.UserNames;

        // Longest first, so "Sam Smith" goes before "Sam" and a full host name before its 15-character form.
        // One-letter names are left to the profile-path rule: replacing them everywhere would mangle ordinary text.
        var all = domains.OfType<string>().Select(d => (Name: d, Replacement: "DOMAIN"))
            .Concat(hosts.OfType<string>().Select(h => (Name: h, Replacement: "PC")))
            .Concat(users.Select(u => (Name: u, Replacement: "USER")))
            .Where(x => x.Name.Length >= 2)
            .DistinctBy(x => x.Name.ToUpperInvariant())
            .OrderByDescending(x => x.Name.Length);
        foreach (var (name, replacement) in all)
        {
            _names.Add((WholeWord(name), replacement));
        }
    }

    public string Redact(string text)
    {
        var result = ComputerElement.Replace(text, "<Computer>PC</Computer>");
        result = AccountSid.Replace(result, "S-1-5-REDACTED");
        result = Email.Replace(result, "user@example.invalid");
        foreach (var (pattern, replacement) in _names)
        {
            result = pattern.Replace(result, replacement);
        }

        // Last, so known names with spaces are already whole; this catches profiles of people not on this PC.
        return ProfilePath.Replace(result, "$1USER");
    }

    private static Regex WholeWord(string word) =>
        new($@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
}
