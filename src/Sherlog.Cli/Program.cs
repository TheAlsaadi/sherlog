using System.Reflection;
using Sherlog.Cli;
using Sherlog.Core.Bundles;
using Sherlog.Core.Evidence;

var version = typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

var env = new CliEnvironment(
    OpenSource: path => path is null ? new LiveRecordSource() : BundleReader.Open(path).Records,
    Out: Console.Out,
    Error: Console.Error,
    Clock: TimeProvider.System,
    LibraryRoot: Path.Combine(AppContext.BaseDirectory, "library"),
    ReadLine: Console.ReadLine,
    TimeZone: TimeZoneInfo.Local,
    SystemInfo: SystemSummary.ForThisPc,
    Redaction: RedactionTargets.ForThisPc,
    Version: version,
    DefaultBundleFolder: () => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) is { Length: > 0 } desktop
        ? desktop
        : Environment.CurrentDirectory);

return CliApp.Run(args, env);
