namespace Sherlog.Core.Evidence;

/// <summary>The evidence sources a recipe's <c>source</c> field can name.</summary>
public static class EvidenceSources
{
    /// <summary>Application log, "Application Error", event 1000.</summary>
    public const string ApplicationError = "application-error";

    /// <summary>Application log, "Application Hang", event 1002.</summary>
    public const string ApplicationHang = "application-hang";

    /// <summary>Application log, "Windows Error Reporting", event 1001 (reports other than plain app crashes).</summary>
    public const string ErrorReport = "wer";

    /// <summary>System log, "Display", event 4101 (graphics driver stopped responding and recovered).</summary>
    public const string DisplayReset = "display-reset";

    /// <summary>System log, "BugCheck" (WER-SystemErrorReporting), event 1001 (blue screen).</summary>
    public const string BugCheck = "bugcheck";

    /// <summary>System log, "Microsoft-Windows-Kernel-Power", event 41 (unexpected shutdown).</summary>
    public const string UnexpectedShutdown = "kernel-power";

    /// <summary>System log, "Application Popup", event 26, when it names an app that couldn't start because a DLL is missing.</summary>
    public const string AppPopup = "app-popup";

    /// <summary>Reliability Monitor records (WMI <c>Win32_ReliabilityRecords</c>).</summary>
    public const string Reliability = "reliability";

    public static IReadOnlyList<string> All { get; } =
    [
        ApplicationError, ApplicationHang, ErrorReport, DisplayReset, BugCheck, UnexpectedShutdown, AppPopup, Reliability,
    ];
}
