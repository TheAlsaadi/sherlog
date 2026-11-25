using Sherlog.Cli;
using Sherlog.Core.Evidence;

var env = new CliEnvironment(
    OpenSource: path => path is null
        ? new LiveRecordSource()
        : throw new UserErrorException("Reading evidence bundles isn't available yet."),
    Out: Console.Out,
    Error: Console.Error,
    Clock: TimeProvider.System,
    LibraryRoot: Path.Combine(AppContext.BaseDirectory, "library"),
    ReadLine: Console.ReadLine,
    TimeZone: TimeZoneInfo.Local);

return CliApp.Run(args, env);
