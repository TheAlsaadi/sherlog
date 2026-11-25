using System.Globalization;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;

namespace Sherlog.Core.Bundles;

/// <summary>
/// The short multiple-choice conversation <c>sherlog collect</c> has with the PC's owner (ADR 0006).
/// Final wording comes from the Phase 1 wording pass.
/// </summary>
public static class OwnerQuestions
{
    public const string WhatCrashedQuestion = "Which crash bothers you most?";
    public const string DoingQuestion = "What were you doing when it happened?";
    public const string ChangesQuestion = "Did anything change on the PC before it started?";
    public const string TriedQuestion = "What have you tried so far?";
    public const string HelpedQuestion = "Did that fix it?";
    public const string StillQuestion = "Does it still happen?";
    public const string NoteQuestion = "Anything else that might help? (for example: it only happens in one game)";

    public static readonly IReadOnlyList<string> DoingChoices =
        ["Playing a game", "Watching a video", "Working or browsing", "The PC was idle", "Starting up or shutting down", "I don't remember"];

    public static readonly IReadOnlyList<string> ChangeChoices =
        ["New graphics driver", "Windows update", "New app or game", "Overclock, BIOS or XMP change", "New hardware", "Nothing I know of", "Not sure"];

    public static readonly IReadOnlyList<string> TriedChoices =
        ["Updated a driver", "Rolled back a driver", "Reinstalled or checked the game files", "Turned off an overlay", "Removed an overclock", "Reinstalled Windows", "Nothing yet"];

    public static readonly IReadOnlyList<string> HelpedChoices = ["Yes", "Partly", "No", "I haven't tried anything"];

    public static readonly IReadOnlyList<string> StillChoices = ["Yes, often", "Sometimes", "Not since the fix", "Not sure"];

    private const string SomethingElse = "Something else";

    public static OwnerAnswers Ask(TextWriter output, Func<string?> readLine, IReadOnlyList<CrashGroup> groups)
    {
        var ended = false;
        string? Read()
        {
            if (ended)
            {
                return null;
            }

            var line = readLine();
            ended = line is null;
            return line;
        }

        output.WriteLine("A few quick questions help find the cause. Press Enter to skip any of them.");
        output.WriteLine();

        var crashChoices = groups.Take(9).Select(g => $"{Label(g)} ({g.Id})").Append(SomethingElse).ToList();
        var whatCrashed = One(output, Read, $"1. {WhatCrashedQuestion}", crashChoices);
        var doing = One(output, Read, $"2. {DoingQuestion}", DoingChoices);
        var changes = Many(output, Read, $"3. {ChangesQuestion}", ChangeChoices);
        var tried = Many(output, Read, $"4. {TriedQuestion}", TriedChoices);
        var helped = One(output, Read, $"5. {HelpedQuestion}", HelpedChoices);
        var still = One(output, Read, $"6. {StillQuestion}", StillChoices);
        output.WriteLine($"7. {NoteQuestion}");
        output.Write("   ");
        var note = Read()?.Trim();

        return new OwnerAnswers(whatCrashed, doing, changes, tried, helped, still, string.IsNullOrEmpty(note) ? null : note);
    }

    /// <summary>A short name for a crash group that a person would recognize.</summary>
    public static string Label(CrashGroup group)
    {
        var c = group.Latest;
        var times = group.Crashes.Count == 1 ? "once" : $"{group.Crashes.Count} times";
        var name = CrashKind.Of(c) switch
        {
            CrashKind.App or CrashKind.Hang => c.AppName ?? "An app",
            CrashKind.Display => $"Graphics driver {c.FaultingModule}",
            CrashKind.BugCheck => $"Blue screen {c.BugCheckCode}",
            CrashKind.Shutdown => "Unexpected shutdown",
            _ => "Other problem",
        };
        return $"{name}, {times}";
    }

    private static string? One(TextWriter output, Func<string?> read, string question, IReadOnlyList<string> choices)
    {
        Show(output, question, choices, "Type a number: ");
        var picked = Numbers(read(), choices.Count).FirstOrDefault(-1);
        return picked < 0 ? null : choices[picked];
    }

    private static List<string> Many(TextWriter output, Func<string?> read, string question, IReadOnlyList<string> choices)
    {
        Show(output, question, choices, "Type one or more numbers: ");
        return Numbers(read(), choices.Count).Distinct().Select(i => choices[i]).ToList();
    }

    private static void Show(TextWriter output, string question, IReadOnlyList<string> choices, string prompt)
    {
        output.WriteLine(question);
        for (var i = 0; i < choices.Count; i++)
        {
            output.WriteLine($"   {i + 1}) {choices[i]}");
        }

        output.Write($"   {prompt}");
    }

    // Accepts "1", "1 2", "1,2"; ignores anything that isn't a listed number.
    private static IEnumerable<int> Numbers(string? line, int count) =>
        (line ?? string.Empty)
            .Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n - 1 : -1)
            .Where(i => i >= 0 && i < count);
}
