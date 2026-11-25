using System.Net;
using System.Net.NetworkInformation;

namespace Sherlog.Core.Bundles;

/// <summary>The names that identify this PC and its people, to be replaced before evidence leaves the PC (ADR 0005).</summary>
/// <param name="MachineName">The PC's NetBIOS name (cut to 15 characters by Windows).</param>
/// <param name="DomainName">The Windows (NetBIOS) domain, when the PC is in one.</param>
/// <param name="HostNames">Other names of this PC, such as its full DNS host name.</param>
/// <param name="DnsDomains">DNS domain names, such as corp.contoso.com.</param>
public sealed record RedactionTargets(
    string MachineName,
    string? DomainName,
    IReadOnlyList<string> UserNames,
    IReadOnlyList<string>? HostNames = null,
    IReadOnlyList<string>? DnsDomains = null)
{
    private static readonly HashSet<string> SharedProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Public", "Default", "Default User", "All Users", "defaultuser0", "WDAGUtilityAccount",
    };

    /// <summary>
    /// This PC's names (NetBIOS and DNS host name), its domains, the signed-in user and every local profile folder name.
    /// </summary>
    public static RedactionTargets ForThisPc()
    {
        var names = new List<string> { Environment.UserName };
        var profilesRoot = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (profilesRoot is not null && Directory.Exists(profilesRoot))
        {
            try
            {
                names.AddRange(Directory.GetDirectories(profilesRoot)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Where(n => !SharedProfiles.Contains(n)));
            }
            catch (UnauthorizedAccessException)
            {
                // The signed-in user is still covered; other profiles are still caught by the profile-path rule.
            }
        }

        var domain = Environment.UserDomainName;
        return new RedactionTargets(
            Environment.MachineName,
            string.Equals(domain, Environment.MachineName, StringComparison.OrdinalIgnoreCase) ? null : domain,
            names.Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NonEmpty(Dns.GetHostName()),
            NonEmpty(IPGlobalProperties.GetIPGlobalProperties().DomainName));
    }

    private static List<string> NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
}
