using Sherlog.Core.Grouping;
using Sherlog.Core.Recipes;

namespace Sherlog.Core.Diagnosis;

/// <summary>Sherlog's explanation of one crash group.</summary>
/// <param name="Recipe">The recipe that explains it, or null when none does.</param>
/// <param name="WhatWeKnow">Plain facts from the evidence: what failed, where, and how often.</param>
public sealed record Diagnosis(
    CrashGroup Group,
    Recipe? Recipe,
    ConfidenceLevel Confidence,
    string Summary,
    string? Details,
    IReadOnlyList<FixSuggestion> Fixes,
    string WhatWeKnow);
