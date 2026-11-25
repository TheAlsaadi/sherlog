using System.CommandLine;
using Sherlog.Cli.Output;

namespace Sherlog.Cli.Commands;

/// <summary><c>sherlog crashes</c>: list crashes, grouped by cause.</summary>
internal static class CrashesCommand
{
    public static Command Create(CliEnvironment env)
    {
        var days = CommonOptions.Days(30);
        var bundle = CommonOptions.Bundle();
        var library = CommonOptions.Library();
        var json = CommonOptions.Json();
        var command = new Command("crashes", "List recent crashes, grouped by cause.") { days, bundle, library, json };

        command.SetAction(result => CliApp.Guard(env, () =>
        {
            var dayCount = result.GetValue(days);
            var diagnoses = Analysis.Run(env, result.GetValue(bundle), result.GetValue(library), dayCount);
            if (result.GetValue(json))
            {
                JsonOutput.Write(env.Out, diagnoses.Select(JsonOutput.Summary).ToArray());
            }
            else
            {
                TextRenderer.Crashes(env.Out, diagnoses, dayCount, env.TimeZone);
            }

            return 0;
        }));
        return command;
    }
}
