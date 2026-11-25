using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using Sherlog.Core.Recipes;
using static Sherlog.Core.Tests.Diagnosis.Make;

namespace Sherlog.Core.Tests.Diagnosis;

public class DiagnoserTests
{
    private static Diagnoser For(params Recipe[] recipes) => new(recipes, TimeZoneInfo.Utc);

    [Fact]
    public void Matching_recipe_gives_its_summary_fixes_and_default_confidence()
    {
        var recipe = Recipe("overlay", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")]);

        var diagnosis = For(recipe).Diagnose(Group(AppCrash(T0)));

        Assert.Same(recipe, diagnosis.Recipe);
        Assert.Equal(ConfidenceLevel.Likely, diagnosis.Confidence);
        Assert.Equal("Summary of overlay.", diagnosis.Summary);
        Assert.Equal(["fix-1"], diagnosis.Fixes.Select(f => f.Id));
    }

    [Fact]
    public void Most_specific_recipe_wins()
    {
        var broad = Recipe("broad", [When(EvidenceSources.ApplicationError, module: "*.dll")]);
        var specific = Recipe("specific", [When(EvidenceSources.ApplicationError, app: "SherlogTestGame.exe", module: "FakeOverlay64.dll")]);

        var diagnosis = For(broad, specific).Diagnose(Group(AppCrash(T0)));

        Assert.Equal("specific", diagnosis.Recipe?.Id);
    }

    [Fact]
    public void Specificity_counts_only_the_condition_that_matched()
    {
        var recipe = Recipe("two-conditions",
        [
            When(EvidenceSources.ApplicationError, app: "Other.exe", module: "FakeOverlay64.dll", code: "0xc0000005"),
            When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll"),
        ]);
        var rival = Recipe("rival", [When(EvidenceSources.ApplicationError, app: "SherlogTestGame.exe", module: "FakeOverlay64.dll")]);

        var diagnosis = For(recipe, rival).Diagnose(Group(AppCrash(T0)));

        Assert.Equal("rival", diagnosis.Recipe?.Id);
    }

    [Fact]
    public void Priority_breaks_specificity_tie()
    {
        var fallback = Recipe("a-fallback", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")], priority: 900);
        var preferred = Recipe("z-preferred", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")], priority: 10);

        Assert.Equal("z-preferred", For(fallback, preferred).Diagnose(Group(AppCrash(T0))).Recipe?.Id);
    }

    [Fact]
    public void Id_breaks_a_full_tie()
    {
        var b = Recipe("b", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")]);
        var a = Recipe("a", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")]);

        Assert.Equal("a", For(b, a).Diagnose(Group(AppCrash(T0))).Recipe?.Id);
    }

    [Fact]
    public void Three_in_seven_days_is_confirmed()
    {
        var recipe = Recipe("r", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")],
            confirmedWhen: new RepeatRule(3, 7));

        var group = Group(AppCrash(T0), AppCrash(T0.AddDays(-3)), AppCrash(T0.AddDays(-6)));

        Assert.Equal(ConfidenceLevel.Confirmed, For(recipe).Diagnose(group).Confidence);
    }

    [Fact]
    public void Three_in_ten_days_stays_likely()
    {
        var recipe = Recipe("r", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")],
            confirmedWhen: new RepeatRule(3, 7));

        var group = Group(AppCrash(T0), AppCrash(T0.AddDays(-5)), AppCrash(T0.AddDays(-10)));

        Assert.Equal(ConfidenceLevel.Likely, For(recipe).Diagnose(group).Confidence);
    }

    [Fact]
    public void Window_ends_at_latest_crash_not_now()
    {
        // Crashes from a year ago still confirm: a bundle diagnosed later must get the same answer.
        var old = T0.AddYears(-1);
        var recipe = Recipe("r", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")],
            confirmedWhen: new RepeatRule(2, 7));

        Assert.Equal(ConfidenceLevel.Confirmed, For(recipe).Diagnose(Group(AppCrash(old), AppCrash(old.AddDays(-1)))).Confidence);
    }

    [Fact]
    public void Unclear_default_stays_unclear_until_confirmed()
    {
        var recipe = Recipe("r", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")], confidence: ConfidenceLevel.Unclear);

        var diagnosis = For(recipe).Diagnose(Group(AppCrash(T0)));

        Assert.Equal(ConfidenceLevel.Unclear, diagnosis.Confidence);
        Assert.Equal("r", diagnosis.Recipe?.Id);
    }

    [Fact]
    public void No_match_is_unclear_and_says_what_we_know()
    {
        var group = Group(AppCrash(T0), AppCrash(T0.AddDays(-17)));

        var diagnosis = For().Diagnose(group);

        Assert.Null(diagnosis.Recipe);
        Assert.Equal(ConfidenceLevel.Unclear, diagnosis.Confidence);
        Assert.Empty(diagnosis.Fixes);
        Assert.Contains("SherlogTestGame.exe", diagnosis.Summary);
        Assert.Contains("SherlogTestGame.exe closed unexpectedly 2 times since Nov 3.", diagnosis.WhatWeKnow);
        Assert.Contains("FakeOverlay64.dll", diagnosis.WhatWeKnow);
        Assert.Contains("0xc0000005", diagnosis.WhatWeKnow);
    }

    [Fact]
    public void What_we_know_is_also_given_for_matched_recipes()
    {
        var recipe = Recipe("r", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")]);

        var diagnosis = For(recipe).Diagnose(Group(AppCrash(T0)));

        Assert.Contains("SherlogTestGame.exe closed unexpectedly once on Nov 20.", diagnosis.WhatWeKnow);
    }

    [Fact]
    public void What_we_know_describes_each_kind_of_crash()
    {
        var reset = new CrashEvent("sys-1", T0, EvidenceSources.DisplayReset, "Display", 4101, null, null, "nvlddmkm", null, null, null, null, null);
        var blue = new CrashEvent("sys-2", T0, EvidenceSources.BugCheck, "x", 1001, null, null, null, null, null, "0x00000116", null, null);
        var power = new CrashEvent("sys-3", T0, EvidenceSources.UnexpectedShutdown, "x", 41, null, null, null, null, null, "0x00000000", null, null);
        var hang = AppCrash(T0, module: null, code: null, source: EvidenceSources.ApplicationHang, eventId: 1002);
        var diagnoser = For();

        Assert.Contains("graphics driver (nvlddmkm)", diagnoser.Diagnose(Group(reset)).WhatWeKnow);
        Assert.Contains("blue screen", diagnoser.Diagnose(Group(blue)).WhatWeKnow);
        Assert.Contains("0x00000116", diagnoser.Diagnose(Group(blue)).WhatWeKnow);
        Assert.Contains("without a blue screen", diagnoser.Diagnose(Group(power)).WhatWeKnow);
        Assert.Contains("stopped responding", diagnoser.Diagnose(Group(hang)).WhatWeKnow);
    }

    [Fact]
    public void DiagnoseAll_keeps_group_order()
    {
        var groups = Sherlog.Core.Grouping.CrashGrouper.Group(
            [AppCrash(T0, app: "A.exe"), AppCrash(T0.AddDays(1), app: "B.exe")]);

        var diagnoses = For().DiagnoseAll(groups);

        Assert.Equal(groups.Select(g => g.Id), diagnoses.Select(d => d.Group.Id));
    }
}
