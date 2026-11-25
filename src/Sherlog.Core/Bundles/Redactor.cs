using System.Text.RegularExpressions;

namespace Sherlog.Core.Bundles;

/// <summary>
/// Replaces anything that identifies a person or PC with fixed placeholders (ADR 0005):
/// profile-folder names in paths become <c>USER</c>, account SIDs <c>S-1-5-REDACTED</c>, email addresses
/// <c>user@example.invalid</c>, the domain <c>DOMAIN</c>, the PC name <c>PC</c> and user names <c>USER</c>.
/// </summary>
public sealed class Redactor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    // A drive, "Users", then the profile folder name; separators may be \, \\ (escaped) or /.
    private static readonly Regex ProfilePath = new(
        @"(?i)([a-z]:(?:\\{1,2}|/)users(?:\\{1,2}|/))[^\\/""<>\r\n]+", RegexOptions.CultureInvariant, Timeout);

    // Local and domain account SIDs (S-1-5-21-…) and Microsoft Entra ID SIDs (S-1-12-1-…). Well-known SIDs stay.
    private static readonly Regex AccountSid = new(@"S-1-5-21-\d+(?:-\d+){2,4}|S-1-12-1-\d+(?:-\d+){3}", RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex Email = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant, Timeout);

    private readonly List<(Regex Pattern, string Replacement)> _names = [];

    public Redactor(RedactionTargets targets)
    {
        if (targets.DomainName is { Length: > 0 } domain)
        {
            _names.Add((WholeWord(domain), "DOMAIN"));
        }

        _names.Add((WholeWord(targets.MachineName), "PC"));

        // Longest first so "Sam Smith" is replaced before "Sam". Very short names are left to the path rule,
        // because replacing them everywhere would mangle ordinary words.
        foreach (var user in targets.UserNames.Where(u => u.Length >= 3).OrderByDescending(u => u.Length))
        {
            _names.Add((WholeWord(user), "USER"));
        }
    }

    public string Redact(string text)
    {
        var result = ProfilePath.Replace(text, "$1USER");
        result = AccountSid.Replace(result, "S-1-5-REDACTED");
        result = Email.Replace(result, "user@example.invalid");
        foreach (var (pattern, replacement) in _names)
        {
            result = pattern.Replace(result, replacement);
        }

        return result;
    }

    private static Regex WholeWord(string word) =>
        new($@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
}
