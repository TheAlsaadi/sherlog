using Microsoft.Extensions.Time.Testing;
using Sherlog.Core.Bundles;
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

    public static readonly SystemSummary System = new("Windows 11 Pro", "26100", "Test CPU", 32, [new GpuInfo("Test GPU", "1.0", "2026-11-01")]);

    /// <summary>Where the guided collect (no command) saves its bundle; the real program uses the Desktop.</summary>
    public string DefaultFolder { get; set; } = Path.Combine(Path.GetTempPath(), "sherlog-tests-default-folder-unused");

    public int Run(params string[] args)
    {
        var env = new CliEnvironment(
            OpenSource: path => path switch
            {
                null => Source,
                _ when Bundles.TryGetValue(path, out var b) => b,
                _ => BundleReader.Open(path).Records, // same as the real program
            },
            Out: Out,
            Error: Error,
            Clock: new FakeTimeProvider(Now),
            LibraryRoot: Repo.Path("library"),
            ReadLine: () => Input.Count > 0 ? Input.Dequeue() : null,
            TimeZone: TimeZoneInfo.Utc,
            SystemInfo: () => System,
            Redaction: () => new RedactionTargets("TESTPC", null, ["TESTUSER"]),
            Version: "0.1.0-test",
            DefaultBundleFolder: () => DefaultFolder);
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
