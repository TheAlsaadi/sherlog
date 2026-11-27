using Sherlog.Core.Diagnosis;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Tests.Diagnosis;

/// <summary>Checks the real recipe library: it loads cleanly, and every example lands on its own recipe.</summary>
public class LibraryExamplesTests
{
    private static readonly RecipeLoadResult Library =
        RecipeLoader.LoadFolders([Repo.Path("library", "recipes")], Repo.SchemaPath);

    [Fact]
    public void Library_loads_without_errors()
    {
        Assert.Empty(Library.Errors.Select(e => $"{Path.GetFileName(e.FilePath)}: {e.Message}"));
        Assert.NotEmpty(Library.Recipes);
    }

    [Fact]
    public void Every_example_resolves_to_its_own_recipe()
    {
        var diagnoser = new Diagnoser(Library.Recipes, TimeZoneInfo.Utc);
        var time = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // A match recipe's examples are crashes on their own; a pattern recipe's examples together are one PC (ADR 0013).
        var wrong = (
            from recipe in Library.Recipes.Where(r => r.Pattern is null)
            from example in recipe.Examples.Select((e, i) => (Example: e, Index: i))
            let crash = ToCrash(example.Example, time)
            let got = diagnoser.Diagnose(CrashGrouper.Group([crash])[0]).Recipe?.Id ?? "(no recipe)"
            where got != recipe.Id
            select $"{recipe.Id} example {example.Index + 1} resolved to {got}").ToList();

        wrong.AddRange(
            from recipe in Library.Recipes.Where(r => r.Pattern is not null)
            from d in diagnoser.DiagnoseAll(CrashGrouper.Group(recipe.Examples.Select(e => ToCrash(e, time))))
            where d.Recipe?.Id != recipe.Id
            select $"{recipe.Id} example group {d.Group.Signature} resolved to {d.Recipe?.Id ?? "(no recipe)"}");

        Assert.Empty(wrong);
    }

    // ADR 0013: blue screens with many different stop codes point to hardware or its settings, and the fixes say so.
    [Fact]
    public void Mixed_blue_screen_recipe_is_likely_and_suggests_the_hardware_checks()
    {
        var recipe = Library.Recipes.Single(r => r.Id == "hardware-unstable-mixed-bluescreens");

        Assert.Equal(new StopCodePattern(3, 30), recipe.Pattern);
        Assert.Equal(ConfidenceLevel.Likely, recipe.Confidence.Default);
        Assert.Empty(recipe.Confidence.ConfirmedWhen);
        var fixes = recipe.Fixes.Select(f => f.Id).ToList();
        Assert.Equal(["reset-overclock-and-xmp", "test-memory", "check-temperatures", "check-power-supply"], fixes);
    }

    [Fact]
    public void Mixed_blue_screen_recipe_does_not_take_over_one_repeated_code()
    {
        var diagnoser = new Diagnoser(Library.Recipes, TimeZoneInfo.Utc);
        var time = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var repeats = Enumerable.Range(0, 5)
            .Select(i => ToCrash(new MatchCondition(EvidenceSources.BugCheck, null, null, null, null, null, "0x0000003b", null, null), time.AddHours(-i)));

        var diagnosis = Assert.Single(diagnoser.DiagnoseAll(CrashGrouper.Group(repeats)));

        Assert.Equal("bsod-system-service-exception", diagnosis.Recipe?.Id);
    }

    // ADR 0011: a graphics timeout only says the card didn't answer in time. The driver, an overclock, heat, power or
    // memory can all cause it, so these recipes can't name a cause, and repeats don't make one more certain.
    [Theory]
    [InlineData("gpu-nvidia-driver-reset")]
    [InlineData("gpu-amd-driver-reset")]
    [InlineData("gpu-intel-driver-reset")]
    [InlineData("gpu-live-kernel-video-timeout")]
    [InlineData("gpu-video-tdr-failure")]
    public void Graphics_timeout_recipes_do_not_claim_a_cause(string id)
    {
        var recipe = Library.Recipes.Single(r => r.Id == id);

        Assert.Equal(ConfidenceLevel.Unclear, recipe.Confidence.Default);
        Assert.Empty(recipe.Confidence.ConfirmedWhen);
    }

    // ADR 0011: their fixes must cover the hardware causes too, not only the driver.
    [Theory]
    [InlineData("gpu-nvidia-driver-reset")]
    [InlineData("gpu-amd-driver-reset")]
    [InlineData("gpu-intel-driver-reset")]
    [InlineData("gpu-live-kernel-video-timeout")]
    [InlineData("gpu-video-tdr-failure")]
    public void Graphics_timeout_recipes_suggest_the_hardware_checks(string id)
    {
        var fixes = Library.Recipes.Single(r => r.Id == id).Fixes.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("reset-gpu-tuning", fixes);
        Assert.Contains("check-temperatures", fixes);
        Assert.Contains("test-memory", fixes);
    }

    private static CrashEvent ToCrash(MatchCondition e, DateTimeOffset time) => new(
        Id: "example",
        Time: time,
        Source: e.Source,
        Provider: e.Provider,
        EventId: e.EventId ?? DefaultEventId(e.Source),
        AppName: e.AppName,
        AppVersion: null,
        FaultingModule: e.FaultingModule,
        ModuleVersion: null,
        ExceptionCode: e.ExceptionCode,
        BugCheckCode: e.BugCheckCode,
        EventName: e.EventName,
        Message: e.MessageContains);

    private static int? DefaultEventId(string source) => source switch
    {
        EvidenceSources.ApplicationError => 1000,
        EvidenceSources.ApplicationHang => 1002,
        EvidenceSources.ErrorReport or EvidenceSources.BugCheck => 1001,
        EvidenceSources.DisplayReset => 4101,
        EvidenceSources.UnexpectedShutdown => 41,
        _ => null,
    };
}
