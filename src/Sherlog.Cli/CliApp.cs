using System.CommandLine;
using Sherlog.Cli.Commands;

namespace Sherlog.Cli;

/// <summary>Builds and runs the <c>sherlog</c> command line. Exit codes: 0 done, 1 bad input, 2 unexpected error.</summary>
public static class CliApp
{
    public static RootCommand Build(CliEnvironment env)
    {
        var root = new RootCommand(
            "Sherlog explains why your PC crashed, in plain language. It doesn't change anything on your PC. " +
            "Run it with no command (or double-click sherlog.exe) to answer a few questions and save a file to share on your Desktop.")
        {
            CrashesCommand.Create(env),
            ExplainCommand.Create(env),
            CollectCommand.Create(env),
            EvalCommand.Create(env),
            RecipesCommand.Create(env),
        };
        root.SetAction(_ => GuidedCollect.Run(env));
        return root;
    }

    public static int Run(string[] args, CliEnvironment env) =>
        Build(env).Parse(args).Invoke(new InvocationConfiguration { Output = env.Out, Error = env.Error });

    /// <summary>Runs a command body, turning person-facing errors into exit 1 and anything else into exit 2.</summary>
    internal static int Guard(CliEnvironment env, Func<int> body)
    {
        try
        {
            return body();
        }
        catch (UserErrorException ex)
        {
            env.Error.WriteLine(ex.Message);
            return 1;
        }
#pragma warning disable CA1031 // Last-resort handler: report any failure plainly instead of a stack trace.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            env.Error.WriteLine($"Something went wrong: {ex.Message}");
            return 2;
        }
    }
}
