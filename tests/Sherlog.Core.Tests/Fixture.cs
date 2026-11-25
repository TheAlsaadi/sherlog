using Sherlog.Core.Evidence;

namespace Sherlog.Core.Tests;

/// <summary>Loads synthetic fixtures copied next to the test assembly.</summary>
internal static class Fixture
{
    public static string Path(params string[] parts) =>
        System.IO.Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    public static string Text(params string[] parts) => File.ReadAllText(Path(parts));

    public static RawEventRecord Event(string log, string file, string? message = null) =>
        new(log, Text("events", file), message);
}
