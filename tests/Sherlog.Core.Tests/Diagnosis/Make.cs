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

    public static MatchCondition When(string source, string? app = null, string? module = null, string? code = null,
        string? bugcheck = null, string? message = null) =>
        new(source, null, null, app, module, code, bugcheck, null, message);

    public static Recipe Recipe(string id, MatchCondition[] match, int priority = 100,
        ConfidenceLevel confidence = ConfidenceLevel.Likely, params RepeatRule[] confirmedWhen) =>
        new(id, 1, id, "crash", ["https://example.org"], match, new ConfidenceRules(confidence, confirmedWhen),
            $"Summary of {id}.", null, priority, [new FixSuggestion("fix-1", "safe", "Do the thing.", ["show_guidance"])],
            match, $"{id}.yaml");
}
