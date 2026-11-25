using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;

namespace Sherlog.Core.Diagnosis;

/// <summary>
/// Explains a crash group no recipe matches: an honest "we don't know yet" plus sensible next steps.
/// Final wording comes from the Phase 1 wording pass.
/// </summary>
public static class UnclearExplainer
{
    public static (string Summary, string Details) Explain(CrashGroup group)
    {
        var latest = group.Latest;
        var repeated = group.Crashes.Count > 1;
        var app = latest.AppName ?? "this app";

        return CrashKind.Of(latest) switch
        {
            CrashKind.App => (
                repeated ? $"We don't know yet why {app} keeps closing." : $"We don't know yet why {app} closed.",
                $"Check for updates to {app} and to your graphics driver. If it was a game, use its launcher to check the game files. "
                + "If it keeps happening, the details below help someone find the cause."),
            CrashKind.Hang => (
                $"We don't know yet why {app} stopped responding.",
                $"Check for updates to {app}. If it keeps happening, note what you were doing when it froze."),
            CrashKind.Display => (
                "We don't know yet why your graphics driver stopped responding.",
                "Install the latest driver from your graphics card maker's website. If you overclock your graphics card, try its default speeds."),
            CrashKind.BugCheck => (
                "We don't know yet what caused the blue screen.",
                "Make sure Windows and your drivers are up to date. If blue screens keep happening, the stop code below helps find the cause."),
            CrashKind.Shutdown => (
                "We don't know yet why your PC shut down unexpectedly.",
                "Check that the power cable and power strip are secure. If it happens under load, such as while gaming, the power supply or overheating may be the cause."),
            _ => (
                "We don't know yet what caused this problem.",
                "Make sure Windows and your drivers are up to date."),
        };
    }
}
