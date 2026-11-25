namespace Sherlog.Core.Bundles;

/// <summary>The names that identify this PC and its people, to be replaced before evidence leaves the PC (ADR 0005).</summary>
public sealed record RedactionTargets(string MachineName, string? DomainName, IReadOnlyList<string> UserNames)
{
    private static readonly HashSet<string> SharedProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Public", "Default", "Default User", "All Users", "defaultuser0", "WDAGUtilityAccount",
    };

    /// <summary>This PC's name, its domain (when it has one), the signed-in user and every local profile folder name.</summary>
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
            names.Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }
}
