using Sherlog.Core.Bundles;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;

namespace Sherlog.Core.Tests.Bundles;

public class LabelFileTests
{
    private static readonly LabelFile Sample = new(
        new OwnerAnswers("SherlogTestGame.exe (g-74537a)", "Playing a game", ["New graphics driver", "Windows update"],
            ["Updated a driver"], "No", "Yes, often", "It says \"oops\" \\ then closes"),
        new InvestigatorSection(false,
        [
            new CrashLabel("g-74537a", "SherlogTestGame.exe", "unknown", null, "unknown"),
            new CrashLabel(null, "Other.exe", "gpu-nvidia-driver-reset", "Owner rolled back the driver.", "owner-reported"),
        ]));

    [Fact]
    public void Label_round_trip_and_comments_present()
    {
        var yaml = Sample.ToYaml();

        Assert.Equal(Sample.Owner.RecentChanges, LabelFile.Parse(yaml).Owner.RecentChanges);
        Assert.Equal(Sample.Investigator.Labels, LabelFile.Parse(yaml).Investigator.Labels);
        Assert.Equal(Sample.Owner.Note, LabelFile.Parse(yaml).Owner.Note);
        Assert.Contains("# 2. What were you doing", yaml);
        Assert.Contains("label_strength:", yaml);
        Assert.Contains("true_cause:", yaml);
    }

    [Fact]
    public void Empty_answers_round_trip_as_empty()
    {
        var empty = new LabelFile(OwnerAnswers.Empty, new InvestigatorSection(false, []));

        var parsed = LabelFile.Parse(empty.ToYaml());

        Assert.Null(parsed.Owner.Doing);
        Assert.Empty(parsed.Owner.Tried);
        Assert.Empty(parsed.Investigator.Labels);
    }

    [Fact]
    public void Label_parses_hand_filled_file()
    {
        const string yaml = """
            format: 1
            owner:
              doing: Playing a game
              recent_changes: [New graphics driver]
              helped: Partly
            investigator:
              seeded: true
              labels:
                - app_name: SherlogSeedCrash.exe
                  true_cause: seed-crash-in-module
                  label_strength: seeded
            """;

        var label = LabelFile.Parse(yaml);

        Assert.Equal("Playing a game", label.Owner.Doing);
        Assert.Equal(["New graphics driver"], label.Owner.RecentChanges);
        Assert.True(label.Investigator.Seeded);
        var only = Assert.Single(label.Investigator.Labels);
        Assert.Null(only.GroupId);
        Assert.Equal("SherlogSeedCrash.exe", only.AppName);
        Assert.Equal("seeded", only.LabelStrength);
    }

    [Theory]
    [InlineData("unknown", "maybe")]
    [InlineData("", "unknown")]
    public void Invalid_label_values_are_rejected(string trueCause, string strength)
    {
        var yaml = $"""
            investigator:
              labels:
                - app_name: A.exe
                  true_cause: "{trueCause}"
                  label_strength: "{strength}"
            """;

        Assert.Throws<InvalidDataException>(() => LabelFile.Parse(yaml));
    }

    [Fact]
    public void Yaml_syntax_error_is_invalid_data()
    {
        Assert.Throws<InvalidDataException>(() => LabelFile.Parse("owner: [unclosed"));
    }

    [Fact]
    public void Owner_questions_record_multi_choice_and_skips()
    {
        var groups = CrashGrouper.Group(
        [
            new CrashEvent("app-1", DateTimeOffset.UnixEpoch, EvidenceSources.ApplicationError, "Application Error", 1000,
                "SherlogTestGame.exe", null, "FakeOverlay64.dll", null, "0xc0000005", null, null, null),
            new CrashEvent("sys-1", DateTimeOffset.UnixEpoch.AddDays(1), EvidenceSources.DisplayReset, "Display", 4101,
                null, null, "nvlddmkm", null, null, null, null, null),
        ]);
        // The display reset is newer, so it is listed first.
        var input = new Queue<string?>(["1", "1", "1, 2 9", "", "x", "4", "Happens in big fights"]);
        var output = new StringWriter();

        var answers = OwnerQuestions.Ask(output, () => input.Dequeue(), groups, TimeZoneInfo.Utc);

        Assert.StartsWith("Graphics driver nvlddmkm", answers.WhatCrashed);
        Assert.Equal("Playing a game", answers.Doing);
        Assert.Equal(["New graphics driver", "Windows update"], answers.RecentChanges);
        Assert.Empty(answers.Tried);
        Assert.Null(answers.Helped);
        Assert.Equal("Not sure", answers.StillHappens);
        Assert.Equal("Happens in big fights", answers.Note);
        Assert.Contains("Press Enter to skip", output.ToString());
    }

    [Fact]
    public void Crash_choices_say_when_each_one_last_happened()
    {
        // One app, two different crashes: the choices must be told apart by more than the group id.
        var day = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        static CrashEvent Crash(string id, DateTimeOffset time, string module) =>
            new(id, time, EvidenceSources.ApplicationError, "Application Error", 1000,
                "SherlogTestChat.exe", null, module, null, "0xc0000005", null, null, null);
        var groups = CrashGrouper.Group(
        [
            Crash("app-1", day, "coreclr.dll"),
            Crash("app-2", day.AddDays(50), "KERNELBASE.dll"),
            Crash("app-3", day.AddDays(51), "KERNELBASE.dll"),
        ]);
        var input = new Queue<string?>(["2"]);
        var output = new StringWriter();

        var answers = OwnerQuestions.Ask(output, () => input.Count > 0 ? input.Dequeue() : null, groups, TimeZoneInfo.Utc);

        var text = output.ToString();
        Assert.Contains("SherlogTestChat.exe, 2 times, last on Nov 12", text);
        Assert.Contains("SherlogTestChat.exe, once on Sep 22", text);
        Assert.StartsWith("SherlogTestChat.exe, once on Sep 22", answers.WhatCrashed);
    }

    [Fact]
    public void Owner_questions_stop_cleanly_at_end_of_input()
    {
        var answers = OwnerQuestions.Ask(new StringWriter(), () => null, [], TimeZoneInfo.Utc);

        Assert.Equal(OwnerAnswers.Empty, answers);
    }
}
