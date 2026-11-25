using Sherlog.Core.Bundles;
using Sherlog.Core.Evidence;

namespace Sherlog.Cli;

/// <summary>Everything the CLI touches outside itself, so commands can be tested in-process.</summary>
/// <param name="OpenSource">Opens the records to read: null means this PC, otherwise an evidence bundle path.</param>
/// <param name="LibraryRoot">Folder holding <c>recipes/</c> and <c>schema/recipe.schema.json</c>.</param>
/// <param name="ReadLine">Reads one line of the person's input; null at end of input.</param>
/// <param name="TimeZone">Used to show crash times.</param>
/// <param name="SystemInfo">This PC's system summary, for evidence bundles.</param>
/// <param name="Redaction">The names to remove from evidence bundles.</param>
/// <param name="Version">Sherlog's version, recorded in evidence bundles.</param>
public sealed record CliEnvironment(
    Func<string?, IRecordSource> OpenSource,
    TextWriter Out,
    TextWriter Error,
    TimeProvider Clock,
    string LibraryRoot,
    Func<string?> ReadLine,
    TimeZoneInfo TimeZone,
    Func<SystemSummary> SystemInfo,
    Func<RedactionTargets> Redaction,
    string Version);

/// <summary>A problem with what the person asked for. Its message is shown as is, and the exit code is 1.</summary>
public sealed class UserErrorException(string message) : Exception(message);
