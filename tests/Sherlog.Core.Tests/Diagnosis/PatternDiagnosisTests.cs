using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;
using static Sherlog.Core.Tests.Diagnosis.Make;

namespace Sherlog.Core.Tests.Diagnosis;

/// <summary>Recipes with a <c>pattern</c> across a PC's blue screens (ADR 0013).</summary>
public class PatternDiagnosisTests
{
    private const string Irql = "0x0000000a";
    private const string Service = "0x0000003b";
    private const string PageFault = "0x00000050";
    private const string Whea = "0x00000124";

    private static readonly Recipe Mixed = PatternRecipe("mixed");

    // A specific single-code recipe for every code used here, so a test shows the pattern beating them.
    private static readonly Recipe[] SingleCode =
    [
        Recipe("irql", [When(EvidenceSources.BugCheck, bugcheck: Irql), When(EvidenceSources.UnexpectedShutdown, bugcheck: Irql)]),
        Recipe("service", [When(EvidenceSources.BugCheck, bugcheck: Service)]),
        Recipe("page-fault", [When(EvidenceSources.BugCheck, bugcheck: PageFault), When(EvidenceSources.UnexpectedShutdown, bugcheck: PageFault)]),
        Recipe("whea", [When(EvidenceSources.BugCheck, bugcheck: Whea)]),
        Recipe("overlay", [When(EvidenceSources.ApplicationError, module: "FakeOverlay64.dll")]),
    ];

    private static IReadOnlyList<Sherlog.Core.Diagnosis.Diagnosis> DiagnoseAll(params CrashEvent[] crashes) =>
        new Diagnoser([Mixed, .. SingleCode], TimeZoneInfo.Utc).DiagnoseAll(CrashGrouper.Group(crashes));

    private static string? RecipeFor(IReadOnlyList<Sherlog.Core.Diagnosis.Diagnosis> all, string code) =>
        all.Single(d => d.Group.Latest.BugCheckCode == code).Recipe?.Id;

    [Fact]
    public void Three_different_codes_explain_every_blue_screen_and_beat_single_code_recipes()
    {
        var all = DiagnoseAll(BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service), BlueScreen(T0.AddDays(-2), PageFault));

        Assert.Equal(3, all.Count);
        Assert.All(all, d =>
        {
            Assert.Equal("mixed", d.Recipe?.Id);
            Assert.Equal(ConfidenceLevel.Likely, d.Confidence);
            Assert.Equal("Summary of mixed.", d.Summary);
        });
    }

    [Fact]
    public void One_code_repeated_does_not_count()
    {
        var all = DiagnoseAll(Enumerable.Range(0, 6).Select(i => BlueScreen(T0.AddHours(-i), Irql)).ToArray());

        Assert.Equal("irql", Assert.Single(all).Recipe?.Id);
    }

    [Fact]
    public void Two_codes_do_not_count_however_often_they_repeat()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Irql), BlueScreen(T0.AddDays(-2), Irql),
            BlueScreen(T0.AddDays(-3), Service), BlueScreen(T0.AddDays(-4), Service));

        Assert.Equal("irql", RecipeFor(all, Irql));
        Assert.Equal("service", RecipeFor(all, Service));
    }

    [Fact]
    public void Codes_spread_outside_the_window_do_not_count()
    {
        var all = DiagnoseAll(BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-20), Service), BlueScreen(T0.AddDays(-40), PageFault));

        Assert.DoesNotContain(all, d => d.Recipe?.Id == "mixed");
    }

    [Fact]
    public void Window_ends_at_the_latest_blue_screen_not_today()
    {
        // Three codes within 30 days of each other, a year before T0: still the pattern.
        var yearAgo = T0.AddDays(-365);
        var all = DiagnoseAll(BlueScreen(yearAgo, Irql), BlueScreen(yearAgo.AddDays(-29), Service), BlueScreen(yearAgo.AddDays(-10), PageFault));

        Assert.All(all, d => Assert.Equal("mixed", d.Recipe?.Id));
    }

    [Fact]
    public void Blue_screen_groups_outside_the_window_keep_their_own_diagnosis()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service), BlueScreen(T0.AddDays(-2), PageFault),
            BlueScreen(T0.AddDays(-60), Whea));

        Assert.Equal("mixed", RecipeFor(all, Irql));
        Assert.Equal("whea", RecipeFor(all, Whea));
    }

    [Fact]
    public void A_group_with_one_crash_in_the_window_is_explained_by_the_pattern()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service),
            BlueScreen(T0.AddDays(-2), PageFault), BlueScreen(T0.AddDays(-90), PageFault));

        Assert.Equal("mixed", RecipeFor(all, PageFault));
    }

    [Fact]
    public void Live_kernel_reports_and_power_loss_are_not_blue_screens()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service),
            LiveKernel(T0.AddDays(-2)), LiveKernel(T0.AddDays(-3), "0x000001a8"),
            BlueScreen(T0.AddDays(-4), "0x00000000", EvidenceSources.UnexpectedShutdown));

        Assert.DoesNotContain(all, d => d.Recipe?.Id == "mixed");
    }

    [Fact]
    public void Live_kernel_reports_are_not_explained_by_the_pattern()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service), BlueScreen(T0.AddDays(-2), PageFault),
            LiveKernel(T0.AddDays(-1)));

        Assert.Null(all.Single(d => d.Group.Latest.Source == EvidenceSources.ErrorReport).Recipe);
    }

    [Fact]
    public void Kernel_power_with_a_stop_code_counts_as_a_blue_screen()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service),
            BlueScreen(T0.AddDays(-2), PageFault, EvidenceSources.UnexpectedShutdown));

        Assert.Equal(3, all.Count);
        Assert.All(all, d => Assert.Equal("mixed", d.Recipe?.Id));
    }

    [Fact]
    public void A_blue_screen_and_its_kernel_power_twin_count_as_one_code()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0, Irql, EvidenceSources.UnexpectedShutdown), BlueScreen(T0.AddDays(-1), Service));

        Assert.DoesNotContain(all, d => d.Recipe?.Id == "mixed");
    }

    [Fact]
    public void Other_kinds_of_crash_keep_their_own_diagnosis()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service), BlueScreen(T0.AddDays(-2), PageFault),
            AppCrash(T0.AddHours(-3)));

        Assert.Equal("overlay", all.Single(d => d.Group.Latest.Source == EvidenceSources.ApplicationError).Recipe?.Id);
    }

    [Fact]
    public void Diagnosing_one_group_alone_never_uses_a_pattern()
    {
        var diagnosis = new Diagnoser([Mixed], TimeZoneInfo.Utc).Diagnose(Group(BlueScreen(T0, Irql)));

        Assert.Null(diagnosis.Recipe);
        Assert.Equal(ConfidenceLevel.Unclear, diagnosis.Confidence);
    }

    [Fact]
    public void Lower_priority_pattern_recipe_wins()
    {
        var crashes = CrashGrouper.Group([BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service), BlueScreen(T0.AddDays(-2), PageFault)]);

        var all = new Diagnoser([PatternRecipe("b-first", priority: 10), PatternRecipe("a-second", priority: 50)], TimeZoneInfo.Utc)
            .DiagnoseAll(crashes);

        Assert.All(all, d => Assert.Equal("b-first", d.Recipe?.Id));
    }

    [Fact]
    public void A_pattern_that_needs_more_codes_does_not_hold_with_fewer()
    {
        var crashes = CrashGrouper.Group([BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Service), BlueScreen(T0.AddDays(-2), PageFault)]);

        var all = new Diagnoser([PatternRecipe("needs-four", count: 4)], TimeZoneInfo.Utc).DiagnoseAll(crashes);

        Assert.All(all, d => Assert.Null(d.Recipe));
    }

    [Fact]
    public void What_we_know_says_how_many_blue_screens_and_codes()
    {
        var all = DiagnoseAll(
            BlueScreen(T0, Irql), BlueScreen(T0.AddDays(-1), Irql), BlueScreen(T0.AddDays(-2), Service),
            BlueScreen(T0.AddDays(-3), PageFault));

        Assert.All(all, d => Assert.EndsWith(
            " This PC had 4 blue screens with 3 different stop codes in the 30 days up to Nov 20.", d.WhatWeKnow));
    }

    [Fact]
    public void Repeats_never_raise_a_pattern_to_confirmed()
    {
        var crashes = Enumerable.Range(0, 10)
            .Select(i => BlueScreen(T0.AddHours(-i), new[] { Irql, Service, PageFault }[i % 3]))
            .ToArray();

        Assert.All(DiagnoseAll(crashes), d => Assert.Equal(ConfidenceLevel.Likely, d.Confidence));
    }
}
