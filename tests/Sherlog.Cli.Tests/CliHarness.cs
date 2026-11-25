using Microsoft.Extensions.Time.Testing;
using Sherlog.Core.Evidence;
using Sherlog.Core.Tests;

namespace Sherlog.Cli.Tests;

/// <summary>Runs the CLI in-process against a fake record source and captures its output.</summary>
internal sealed class CliHarness
{
    public static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    public FakeRecordSource Source { get; } = new();

    public Dictionary<string, IRecordSource> Bundles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public StringWriter Out { get; } = new();

    public StringWriter Error { get; } = new();

    public Queue<string?> Input { get; } = new();

    public int Run(params string[] args)
    {
        var env = new CliEnvironment(
            OpenSource: path => path is null ? Source : Bundles.TryGetValue(path, out var b) ? b : throw new FileNotFoundException(path),
            Out: Out,
            Error: Error,
            Clock: new FakeTimeProvider(Now),
            LibraryRoot: Repo.Path("library"),
            ReadLine: () => Input.Count > 0 ? Input.Dequeue() : null,
            TimeZone: TimeZoneInfo.Utc);
        return CliApp.Run(args, env);
    }

    /// <summary>Folder of test recipes (the valid NVIDIA fixture).</summary>
    public static string ValidRecipes => Fixture.Path("recipes", "valid");

    public static string MixedRecipes => Fixture.Path("recipes", "mixed");

    public void AddNvidiaResets(int count)
    {
        var xml = Fixture.Text("events", "display-4101.xml");
        for (var i = 0; i < count; i++)
        {
            var time = Now.AddDays(-1 - i).UtcDateTime.ToString("o");
            Source.Events.Add(new RawEventRecord("System",
                xml.Replace("2026-11-22T20:15:00Z", time).Replace("<EventRecordID>7001<", $"<EventRecordID>{8000 + i}<"), null));
        }
    }
}
