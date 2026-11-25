using Sherlog.Core.Evidence;

namespace Sherlog.Core.Grouping;

/// <summary>Crashes that share one signature, treated as one problem. Crashes are newest first.</summary>
public sealed record CrashGroup(string Id, string Signature, IReadOnlyList<CrashEvent> Crashes)
{
    public CrashEvent Latest => Crashes[0];
}
