using System.CommandLine;
using Sherlog.Cli.Output;

namespace Sherlog.Cli.Commands;

/// <summary><c>sherlog explain</c>: the plain-language diagnosis of one crash group.</summary>
internal static class ExplainCommand
{
    public static Command Create(CliEnvironment env)
    {
        var id = new Argument<string?>("id")
        {
            Description = "A problem id (g-…) from 'sherlog crashes', or a crash id (app-…, sys-…, rel-…).",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var latest = new Option<bool>("--latest") { Description = "Explain the most recent crash." };
        var days = CommonOptions.Days(30);
        var bundle = CommonOptions.Bundle();
        var library = CommonOptions.Library();
        var json = CommonOptions.Json();
        var command = new Command("explain", "Explain why something crashed and what you can try.") { id, latest, days, bundle, library, json };

        command.SetAction(result => CliApp.Guard(env, () =>
        {
            var wanted = result.GetValue(id);
            var useLatest = result.GetValue(latest);
            if (wanted is null && !useLatest)
            {
                throw new UserErrorException("Give a problem id from 'sherlog crashes', or use --latest.");
            }

            var dayCount = result.GetValue(days);
            var diagnoses = Analysis.Run(env, result.GetValue(bundle), result.GetValue(library), dayCount);
            if (diagnoses.Count == 0)
            {
                env.Out.WriteLine(TextRenderer.NoCrashes(dayCount));
                return 0;
            }

            var diagnosis = useLatest
                ? diagnoses[0]
                : diagnoses.FirstOrDefault(d =>
                      string.Equals(d.Group.Id, wanted, StringComparison.OrdinalIgnoreCase)
                      || d.Group.Crashes.Any(c => string.Equals(c.Id, wanted, StringComparison.OrdinalIgnoreCase)))
                  ?? throw new UserErrorException($"No crash with id {wanted} in the last {dayCount} days. Run 'sherlog crashes' to see the ids.");

            if (result.GetValue(json))
            {
                JsonOutput.Write(env.Out, JsonOutput.Detail(diagnosis));
            }
            else
            {
                TextRenderer.Explain(env.Out, diagnosis);
            }

            return 0;
        }));
        return command;
    }
}
