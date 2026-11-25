using Sherlog.Core.Evidence;

namespace Sherlog.Core.Tests.Evidence;

public class EvidenceReaderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 14, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LongAgo = T0.AddDays(-365);

    [Fact]
    public void Reliability_duplicate_of_app_error_is_dropped()
    {
        var source = new FakeRecordSource()
            .AddAppError(1, T0)
            .AddReliabilityAppFailure(901, T0.AddSeconds(30));

        var crashes = new EvidenceReader(source).ReadCrashes(LongAgo);

        Assert.Equal(["app-1"], crashes.Select(c => c.Id));
    }

    [Fact]
    public void Reliability_without_event_log_twin_is_kept()
    {
        var source = new FakeRecordSource()
            .AddAppError(1, T0)
            .AddReliabilityAppFailure(901, T0.AddMinutes(5))
            .AddReliabilityAppFailure(902, T0.AddSeconds(10), app: "OtherGame.exe");

        var ids = new EvidenceReader(source).ReadCrashes(LongAgo).Select(c => c.Id).ToHashSet();

        Assert.Equal(new HashSet<string> { "app-1", "rel-901", "rel-902" }, ids);
    }

    [Fact]
    public void Reliability_bluescreen_twin_of_bugcheck_event_is_dropped()
    {
        var source = new FakeRecordSource();
        source.Events.Add(Fixture.Event("System", "bugcheck-1001.xml"));
        source.Reliability.Add(new RawReliabilityRecord(
            System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(Fixture.Text("reliability", "windows-failure.json"))!));

        var crashes = new EvidenceReader(source).ReadCrashes(LongAgo);

        Assert.Equal(["sys-7002"], crashes.Select(c => c.Id));
    }

    [Fact]
    public void Results_are_newest_first()
    {
        var source = new FakeRecordSource()
            .AddAppError(1, T0)
            .AddAppError(2, T0.AddHours(2))
            .AddAppError(3, T0.AddHours(1));

        var ids = new EvidenceReader(source).ReadCrashes(LongAgo).Select(c => c.Id);

        Assert.Equal(["app-2", "app-3", "app-1"], ids);
    }

    [Fact]
    public void Events_before_since_are_excluded()
    {
        var source = new FakeRecordSource()
            .AddAppError(1, T0.AddDays(-40))
            .AddAppError(2, T0);

        var ids = new EvidenceReader(source).ReadCrashes(T0.AddDays(-30)).Select(c => c.Id);

        Assert.Equal(["app-2"], ids);
    }

    [Fact]
    public void Unparseable_records_are_skipped()
    {
        var source = new FakeRecordSource().AddAppError(1, T0);
        source.Events.Add(new RawEventRecord("Application", "<not xml", null));
        source.Events.Add(Fixture.Event("Application", "unrelated-app-event.xml"));

        var crashes = new EvidenceReader(source).ReadCrashes(LongAgo);

        Assert.Single(crashes);
    }

    [Fact]
    public void System_log_records_are_parsed_too()
    {
        var source = new FakeRecordSource();
        source.Events.Add(Fixture.Event("System", "display-4101.xml"));

        var crash = Assert.Single(new EvidenceReader(source).ReadCrashes(LongAgo));

        Assert.Equal(EvidenceSources.DisplayReset, crash.Source);
    }

    [Fact]
    public void Source_warnings_are_passed_through()
    {
        var source = new FakeRecordSource();
        source.WarningList.Add("Couldn't read the System log.");
        var reader = new EvidenceReader(source);

        reader.ReadCrashes(LongAgo);

        Assert.Equal(["Couldn't read the System log."], reader.Warnings);
    }
}
