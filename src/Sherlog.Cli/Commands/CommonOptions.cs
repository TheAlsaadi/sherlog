using System.CommandLine;

namespace Sherlog.Cli.Commands;

/// <summary>Options shared by several commands.</summary>
internal static class CommonOptions
{
    public static Option<string[]> Library() => new("--library")
    {
        Description = "A folder of recipes to use instead of the built-in library. Can be given more than once.",
        Arity = ArgumentArity.OneOrMore,
    };

    public static Option<string?> Bundle() => new("--bundle")
    {
        Description = "Read crashes from an evidence bundle (.zip) instead of this PC.",
    };

    public static Option<bool> Json() => new("--json")
    {
        Description = "Print the result as JSON.",
    };

    public static Option<int> Days(int defaultDays) => new("--days")
    {
        Description = "How many days back to look.",
        DefaultValueFactory = _ => defaultDays,
    };
}
