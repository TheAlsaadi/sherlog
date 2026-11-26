using System.CommandLine;
using Sherlog.Core.Bundles;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;

namespace Sherlog.Cli.Commands;

/// <summary><c>sherlog collect</c>: save a redacted evidence bundle that can be shared (ADRs 0005, 0006).</summary>
internal static class CollectCommand
{
    public static Command Create(CliEnvironment env)
    {
        var redact = new Option<bool>("--redact")
        {
            Description = "Replace names that identify you or your PC. Always on; accepted for clarity.",
        };
        var output = new Option<string>("--out")
        {
            Description = "Where to save the bundle.",
            DefaultValueFactory = _ => "sherlog-evidence.zip",
        };
        var days = CommonOptions.Days(90);
        var noQuestions = new Option<bool>("--no-questions") { Description = "Don't ask the questions about the crash." };
        var command = new Command("collect", "Save your crash records in a file you can share, with names removed.")
        {
            redact, output, days, noQuestions,
        };

        command.SetAction(result => CliApp.Guard(env, () =>
        {
            var dayCount = result.GetValue(days);
            if (dayCount < 1)
            {
                throw new UserErrorException("--days must be 1 or more.");
            }

            Collect(env, Path.GetFullPath(result.GetValue(output)!), dayCount, askQuestions: !result.GetValue(noQuestions));
            return 0;
        }));
        return command;
    }

    /// <summary>Reads this PC's records, asks the questions if wanted, and saves the redacted bundle at <paramref name="path"/>.</summary>
    internal static void Collect(CliEnvironment env, string path, int dayCount, bool askQuestions)
    {
        // Check where the file will go before reading anything or asking questions, so answers are never lost.
        var folder = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(folder))
        {
            throw new UserErrorException($"There's no such folder: {folder}");
        }

        if (Directory.Exists(path))
        {
            throw new UserErrorException($"{path} is a folder. Give a file name, for example {Path.Combine(path, "sherlog-evidence.zip")}");
        }

        var now = env.Clock.GetUtcNow();
        var since = now.AddDays(-dayCount);

        // Read once, so the questions, the labels and the bundle all see the same records.
        var live = env.OpenSource(null);
        var snapshot = new BundleRecordSource(live.ReadEvents(since).ToList(), live.ReadReliability(since).ToList(), now);
        foreach (var warning in live.Warnings.Distinct())
        {
            env.Error.WriteLine(warning);
        }

        var groups = CrashGrouper.Group(new EvidenceReader(snapshot).ReadCrashes(since));
        var answers = askQuestions ? OwnerQuestions.Ask(env.Out, env.ReadLine, groups, env.TimeZone) : OwnerAnswers.Empty;
        var labels = groups
            .Select(g => new CrashLabel(g.Id, g.Latest.AppName, "unknown", null, "unknown"))
            .ToList();
        var label = new LabelFile(answers, new InvestigatorSection(false, labels));

        try
        {
            BundleWriter.Write(path, snapshot, since, env.SystemInfo(), label, new Redactor(env.Redaction()), env.Version, now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UserErrorException($"Couldn't save {path}: {ex.Message}");
        }

        var problems = groups.Count == 1 ? "1 problem" : $"{groups.Count} problems";
        env.Out.WriteLine();
        env.Out.WriteLine($"Saved {problems} from the last {dayCount} days to:");
        env.Out.WriteLine($"  {path}");
        env.Out.WriteLine("Sherlog replaced your user name, your PC's name and your account ID with placeholders.");
        env.Out.WriteLine("Open the file if you'd like to check it before you send it.");
    }
}
