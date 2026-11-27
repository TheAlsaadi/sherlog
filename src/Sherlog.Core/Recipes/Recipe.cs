using Sherlog.Core.Diagnosis;

namespace Sherlog.Core.Recipes;

/// <summary>A loaded, validated recipe (format v0, docs/recipe-format.md).</summary>
/// <param name="Match">The conditions for one crash group; empty for a pattern recipe.</param>
/// <param name="Pattern">A pattern across the PC's blue screens (ADR 0013), used instead of <paramref name="Match"/>.</param>
public sealed record Recipe(
    string Id,
    int Version,
    string Title,
    string Category,
    IReadOnlyList<string> References,
    IReadOnlyList<MatchCondition> Match,
    ConfidenceRules Confidence,
    string Summary,
    string? Details,
    int Priority,
    IReadOnlyList<FixSuggestion> Fixes,
    IReadOnlyList<MatchCondition> Examples,
    string FilePath,
    StopCodePattern? Pattern = null);

/// <summary>Evidence fields that must all hold for one crash. Also used for a recipe's example crashes.</summary>
public sealed record MatchCondition(
    string Source,
    string? Provider,
    int? EventId,
    string? AppName,
    string? FaultingModule,
    string? ExceptionCode,
    string? BugCheckCode,
    string? EventName,
    string? MessageContains)
{
    /// <summary>How many fields besides <see cref="Source"/> this condition constrains. More is more specific.</summary>
    public int ConstrainedFieldCount =>
        new object?[] { Provider, EventId, AppName, FaultingModule, ExceptionCode, BugCheckCode, EventName, MessageContains }
            .Count(v => v is not null);
}

public sealed record ConfidenceRules(ConfidenceLevel Default, IReadOnlyList<RepeatRule> ConfirmedWhen);

/// <summary>At least <see cref="Count"/> crashes in the group within <see cref="Days"/> days.</summary>
public sealed record RepeatRule(int Count, int Days);

/// <summary>
/// At least <see cref="Count"/> different non-zero stop codes among the PC's blue screens within <see cref="Days"/> days,
/// ending at its most recent blue screen (ADR 0013).
/// </summary>
public sealed record StopCodePattern(int Count, int Days);

/// <summary>A suggested fix. In Phase 1 it is shown, never run.</summary>
public sealed record FixSuggestion(string Id, string Risk, string Explain, IReadOnlyList<string> Actions);

public sealed record RecipeLoadResult(IReadOnlyList<Recipe> Recipes, IReadOnlyList<RecipeError> Errors);

public sealed record RecipeError(string FilePath, string Message);
