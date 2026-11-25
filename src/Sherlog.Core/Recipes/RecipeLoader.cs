using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Sherlog.Core.Diagnosis;
using YamlDotNet.Core;

namespace Sherlog.Core.Recipes;

/// <summary>
/// Loads recipes from folders of <c>*.yaml</c> files and checks each one against <c>recipe.schema.json</c>.
/// A bad file is reported and skipped; it never stops the others from loading.
/// </summary>
public static class RecipeLoader
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    public static RecipeLoadResult LoadFolders(IEnumerable<string> folders, string schemaPath)
    {
        // A private registry per load: the global one refuses to register the same $id twice.
        var schema = JsonSchema.FromText(File.ReadAllText(schemaPath), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        var recipes = new List<Recipe>();
        var errors = new List<RecipeError>();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        var files = folders
            .Where(Directory.Exists)
            .SelectMany(f => Directory.EnumerateFiles(f, "*.yaml", SearchOption.TopDirectoryOnly))
            .Order(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var (recipe, error) = LoadFile(file, schema);
            if (error is not null)
            {
                errors.Add(new RecipeError(file, error));
            }
            else if (seen.TryGetValue(recipe!.Id, out var firstFile))
            {
                errors.Add(new RecipeError(file, $"Recipe id '{recipe.Id}' is already used by {Path.GetFileName(firstFile)}."));
            }
            else
            {
                seen[recipe.Id] = file;
                recipes.Add(recipe);
            }
        }

        return new RecipeLoadResult(recipes, errors);
    }

    private static (Recipe? Recipe, string? Error) LoadFile(string file, JsonSchema schema)
    {
        JsonNode? node;
        try
        {
            node = YamlJson.Parse(File.ReadAllText(file));
        }
        catch (YamlException ex)
        {
            return (null, $"YAML syntax error at line {ex.Start.Line}: {ex.Message}");
        }

        if (node is null)
        {
            return (null, "The file is empty.");
        }

        using var doc = JsonDocument.Parse(node.ToJsonString());
        var result = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!result.IsValid)
        {
            return (null, FirstError(result));
        }

        var dto = node.Deserialize<RecipeDto>(Json)!;
        return (ToRecipe(dto, file), null);
    }

    private static string FirstError(EvaluationResults result)
    {
        var failures = (result.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .Select(d => $"{Location(d.InstanceLocation.ToString())}: {d.Errors!.Values.First()}")
            .Distinct()
            .ToList();
        return failures.Count == 0 ? "The recipe does not match the recipe schema." : string.Join("; ", failures.Take(3));
    }

    private static string Location(string pointer) => string.IsNullOrEmpty(pointer) ? "(top level)" : pointer;

    private static Recipe ToRecipe(RecipeDto d, string file) => new(
        Id: d.Id,
        Version: d.Version,
        Title: d.Title,
        Category: d.Category,
        References: d.References,
        Match: d.Match.Any.Select(ToCondition).ToList(),
        Confidence: new ConfidenceRules(
            d.Confidence.Default == "likely" ? ConfidenceLevel.Likely : ConfidenceLevel.Unclear,
            (d.Confidence.ConfirmedWhen ?? [])
                .Select(r => new RepeatRule(r.OccurrencesWithinDays.Count, r.OccurrencesWithinDays.Days))
                .ToList()),
        Summary: d.Diagnosis.Summary,
        Details: d.Diagnosis.Details,
        Priority: d.Priority ?? 100,
        Fixes: (d.Fixes ?? []).Select(f => new FixSuggestion(f.Id, f.Risk, f.Explain, f.Actions.Select(a => a.Action).ToList())).ToList(),
        Examples: d.Examples.Select(ToCondition).ToList(),
        FilePath: file);

    private static MatchCondition ToCondition(ConditionDto c) => new(
        c.Source, c.Provider, c.EventId, c.AppName, c.FaultingModule, c.ExceptionCode, c.BugCheckCode, c.EventName, c.MessageContains);

    private sealed record RecipeDto(
        string Id, int Version, string Title, string Category, List<string> References, MatchDto Match,
        ConfidenceDto Confidence, DiagnosisDto Diagnosis, int? Priority, List<FixDto>? Fixes, List<ConditionDto> Examples);

    private sealed record MatchDto(List<ConditionDto> Any);

    private sealed record ConditionDto(
        string Source, string? Provider, int? EventId, string? AppName, string? FaultingModule,
        string? ExceptionCode,
        [property: System.Text.Json.Serialization.JsonPropertyName("bugcheck_code")] string? BugCheckCode,
        string? EventName, string? MessageContains);

    private sealed record ConfidenceDto(string Default, List<RepeatDto>? ConfirmedWhen);

    private sealed record RepeatDto(CountDaysDto OccurrencesWithinDays);

    private sealed record CountDaysDto(int Count, int Days);

    private sealed record DiagnosisDto(string Summary, string? Details);

    private sealed record FixDto(string Id, string Risk, string Explain, List<ActionDto> Actions);

    private sealed record ActionDto(string Action);
}
