using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using static Sherlog.Core.Tests.Diagnosis.Make;

namespace Sherlog.Core.Tests.Diagnosis;

public class ConditionMatcherTests
{
    [Theory]
    [InlineData("FakeOverlay64.dll", true)]
    [InlineData("fakeoverlay64.DLL", true)]
    [InlineData("Fake*.dll", true)]
    [InlineData("*overlay*", true)]
    [InlineData("Fake", false)]
    [InlineData("Other*.dll", false)]
    public void Module_matching_ignores_case_and_supports_wildcards(string pattern, bool expected)
    {
        var condition = When(EvidenceSources.ApplicationError, module: pattern);

        Assert.Equal(expected, ConditionMatcher.Matches(condition, AppCrash(T0)));
    }

    [Fact]
    public void All_fields_in_condition_must_hold()
    {
        var condition = When(EvidenceSources.ApplicationError, app: "SherlogTestGame.exe", code: "0xc0000409");

        Assert.False(ConditionMatcher.Matches(condition, AppCrash(T0)));
    }

    [Fact]
    public void Source_must_match()
    {
        Assert.False(ConditionMatcher.Matches(When(EvidenceSources.DisplayReset, module: "FakeOverlay64.dll"), AppCrash(T0)));
    }

    [Fact]
    public void Message_contains_is_case_insensitive()
    {
        var crash = AppCrash(T0, message: "The Program stopped working");

        Assert.True(ConditionMatcher.Matches(When(EvidenceSources.ApplicationError, message: "program STOPPED"), crash));
        Assert.False(ConditionMatcher.Matches(When(EvidenceSources.ApplicationError, message: "missing"), crash));
    }

    [Fact]
    public void Message_condition_never_matches_a_crash_without_message()
    {
        Assert.False(ConditionMatcher.Matches(When(EvidenceSources.ApplicationError, message: "x"), AppCrash(T0)));
    }

    [Fact]
    public void Reliability_app_crash_matches_an_application_error_condition()
    {
        // When the Application log has rolled over, Reliability Monitor may hold the only record of the crash.
        var fromReliability = AppCrash(T0, source: EvidenceSources.Reliability);

        Assert.True(ConditionMatcher.Matches(When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll"), fromReliability));
        Assert.False(ConditionMatcher.Matches(When(EvidenceSources.ApplicationHang, app: "SherlogTestGame.exe"), fromReliability));
    }
}
