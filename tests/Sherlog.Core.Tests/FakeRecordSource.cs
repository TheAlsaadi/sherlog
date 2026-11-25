using Sherlog.Core.Evidence;

namespace Sherlog.Core.Tests;

/// <summary>An in-memory record source for tests.</summary>
internal sealed class FakeRecordSource : IRecordSource
{
    public List<RawEventRecord> Events { get; } = [];

    public List<RawReliabilityRecord> Reliability { get; } = [];

    public List<string> WarningList { get; } = [];

    public IReadOnlyList<string> Warnings => WarningList;

    public IEnumerable<RawEventRecord> ReadEvents(DateTimeOffset since) => Events;

    public IEnumerable<RawReliabilityRecord> ReadReliability(DateTimeOffset since) => Reliability;

    /// <summary>Adds an Application Error 1000 event built from the named fixture with the given overrides.</summary>
    public FakeRecordSource AddAppError(long recordId, DateTimeOffset time, string app = "SherlogTestGame.exe",
        string module = "FakeOverlay64.dll", string code = "c0000005")
    {
        var xml = Fixture.Text("events", "app-error-1000-named.xml")
            .Replace("<EventRecordID>4242</EventRecordID>", $"<EventRecordID>{recordId}</EventRecordID>")
            .Replace("2026-11-20T14:30:00.1234567Z", time.UtcDateTime.ToString("o"))
            .Replace(">SherlogTestGame.exe<", $">{app}<")
            .Replace(">FakeOverlay64.dll<", $">{module}<")
            .Replace(">c0000005<", $">{code}<");
        Events.Add(new RawEventRecord("Application", xml, null));
        return this;
    }

    /// <summary>Adds a reliability app-failure row built from the fixture with the given overrides.</summary>
    public FakeRecordSource AddReliabilityAppFailure(long recordNumber, DateTimeOffset time, string app = "SherlogTestGame.exe")
    {
        var props = new Dictionary<string, string?>(
            System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(Fixture.Text("reliability", "app-failure.json"))!)
        {
            ["RecordNumber"] = recordNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["TimeGenerated"] = time.UtcDateTime.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".000000-000",
            ["ProductName"] = app,
        };
        props["InsertionStrings"] = props["InsertionStrings"]!.Replace("SherlogTestGame.exe", app);
        Reliability.Add(new RawReliabilityRecord(props));
        return this;
    }
}
