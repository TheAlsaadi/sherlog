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

        var wrong = (
            from recipe in Library.Recipes
            from example in recipe.Examples.Select((e, i) => (Example: e, Index: i))
            let crash = ToCrash(example.Example, time)
            let got = diagnoser.Diagnose(CrashGrouper.Group([crash])[0]).Recipe?.Id ?? "(no recipe)"
            where got != recipe.Id
            select $"{recipe.Id} example {example.Index + 1} resolved to {got}").ToList();

        Assert.Empty(wrong);
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
