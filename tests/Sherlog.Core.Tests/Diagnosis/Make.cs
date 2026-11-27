using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Tests.Diagnosis;

/// <summary>Builders for diagnosis tests.</summary>
internal static class Make
{
    public static readonly DateTimeOffset T0 = new(2026, 11, 20, 14, 30, 0, TimeSpan.Zero);

    public static CrashEvent AppCrash(DateTimeOffset time, string app = "SherlogTestGame.exe", string? module = "FakeOverlay64.dll",
        string? code = "0xc0000005", string source = EvidenceSources.ApplicationError, string? message = null, int eventId = 1000) =>
        new($"app-{time.Ticks}", time, source, "Application Error", eventId, app, "1.0", module, "1.0", code, null, null, message);

    public static CrashGroup Group(params CrashEvent[] crashes) => Assert.Single(CrashGrouper.Group(crashes));

    /// <summary>A blue screen: a BugCheck record, or a Kernel-Power 41 with <paramref name="code"/> as its stop code.</summary>
    public static CrashEvent BlueScreen(DateTimeOffset time, string code, string source = EvidenceSources.BugCheck) =>
        new($"{source}-{code}-{time.Ticks}", time, source,
            source == EvidenceSources.UnexpectedShutdown ? "Microsoft-Windows-Kernel-Power" : "Microsoft-Windows-WER-SystemErrorReporting",
            source == EvidenceSources.UnexpectedShutdown ? 41 : 1001, null, null, null, null, null, code, null, null);

    /// <summary>A Windows Error Reporting live-kernel report (the PC kept running).</summary>
    public static CrashEvent LiveKernel(DateTimeOffset time, string code = "0x00000141") =>
        new($"wer-{code}-{time.Ticks}", time, EvidenceSources.ErrorReport, "Windows Error Reporting", 1001,
            null, null, null, null, null, code, "LiveKernelEvent", null);

    public static Recipe PatternRecipe(string id, int count = 3, int days = 30, int priority = 100,
        ConfidenceLevel confidence = ConfidenceLevel.Likely) =>
        new(id, 1, id, "bluescreen", ["https://example.org"], [], new ConfidenceRules(confidence, []),
            $"Summary of {id}.", null, priority, [new FixSuggestion("fix-1", "safe", "Do the thing.", ["show_guidance"])],
            [], $"{id}.yaml", new StopCodePattern(count, days));

    public static MatchCondition When(string source, string? app = null, string? module = null, string? code = null,
        string? bugcheck = null, string? message = null) =>
        new(source, null, null, app, module, code, bugcheck, null, message);

    public static Recipe Recipe(string id, MatchCondition[] match, int priority = 100,
        ConfidenceLevel confidence = ConfidenceLevel.Likely, params RepeatRule[] confirmedWhen) =>
        new(id, 1, id, "crash", ["https://example.org"], match, new ConfidenceRules(confidence, confirmedWhen),
            $"Summary of {id}.", null, priority, [new FixSuggestion("fix-1", "safe", "Do the thing.", ["show_guidance"])],
            match, $"{id}.yaml");
}
