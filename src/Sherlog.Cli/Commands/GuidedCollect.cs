using System.Globalization;

namespace Sherlog.Cli.Commands;

/// <summary>
/// <c>sherlog</c> with no command: what a friend gets by double-clicking sherlog.exe in the field test.
/// It explains itself, asks the questions, saves the bundle to the Desktop and keeps the window open until Enter.
/// </summary>
internal static class GuidedCollect
{
    public const int Days = 90;

    public static int Run(CliEnvironment env)
    {
        var exit = CliApp.Guard(env, () =>
        {
            env.Out.WriteLine("Sherlog looks at your PC's crash records from the last 90 days and saves them in one file");
            env.Out.WriteLine("you can send to the person who asked you to try it. It doesn't change anything on your PC.");
            env.Out.WriteLine();
            CollectCommand.Collect(env, FreePath(env), Days, askQuestions: true);
            env.Out.WriteLine("Send that file to the person who gave you Sherlog. Thank you!");
            return 0;
        });

        // A double-clicked console window closes as soon as the program ends, so wait even after an error.
        env.Out.WriteLine();
        env.Out.WriteLine("Press Enter to close.");
        env.ReadLine();
        return exit;
    }

    /// <summary>A file name in the default folder that doesn't overwrite an earlier bundle.</summary>
    private static string FreePath(CliEnvironment env)
    {
        var folder = env.DefaultBundleFolder();
        var date = TimeZoneInfo.ConvertTime(env.Clock.GetUtcNow(), env.TimeZone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var path = Path.GetFullPath(Path.Combine(folder, $"sherlog-evidence-{date}.zip"));
        for (var n = 2; File.Exists(path) || Directory.Exists(path); n++)
        {
            path = Path.GetFullPath(Path.Combine(folder, $"sherlog-evidence-{date}-{n}.zip"));
        }

        return path;
    }
}
