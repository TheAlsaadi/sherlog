namespace Sherlog.Core.Evidence;

/// <summary>
/// The kind of failure a crash record describes, independent of where it was recorded.
/// Reliability Monitor rows map onto the same kinds as the event-log records they repeat.
/// </summary>
public static class CrashKind
{
    public const string App = "app";
    public const string Hang = "hang";
    public const string Display = "display";
    public const string BugCheck = "bugcheck";
    public const string Shutdown = "shutdown";
    public const string Report = "wer";
    public const string Popup = "popup";

    public static string Of(CrashEvent c) => c.Source == EvidenceSources.Reliability
        ? c.EventId switch
        {
            1000 => App,
            1002 => Hang,
            1001 => BugCheck,
            _ => EvidenceSources.Reliability,
        }
        : OfSource(c.Source);

    /// <summary>The kind a recipe condition's <c>source</c> names.</summary>
    public static string OfSource(string source) => source switch
    {
        EvidenceSources.ApplicationError => App,
        EvidenceSources.ApplicationHang => Hang,
        EvidenceSources.DisplayReset => Display,
        EvidenceSources.BugCheck => BugCheck,
        EvidenceSources.UnexpectedShutdown => Shutdown,
        EvidenceSources.ErrorReport => Report,
        EvidenceSources.AppPopup => Popup,
        _ => source,
    };
}
