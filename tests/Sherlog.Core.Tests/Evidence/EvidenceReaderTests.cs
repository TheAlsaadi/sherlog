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
    public void One_blue_screen_is_one_crash_not_two()
    {
        // After a blue screen Windows logs both BugCheck 1001 and Kernel-Power 41 with the same code, minutes apart.
        var source = new FakeRecordSource();
        source.Events.Add(Fixture.Event("System", "bugcheck-1001.xml"));                 // 08:00, 0x116
        source.Events.Add(Fixture.Event("System", "kernel-power-41-bugcheck.xml"));       // 08:01, 0x116

        var crashes = new EvidenceReader(source).ReadCrashes(LongAgo);

        Assert.Equal(["sys-7002"], crashes.Select(c => c.Id));
    }

    [Fact]
    public void Power_loss_shutdown_is_kept_next_to_a_blue_screen()
    {
        var source = new FakeRecordSource();
        source.Events.Add(Fixture.Event("System", "bugcheck-1001.xml"));
        source.Events.Add(Fixture.Event("System", "kernel-power-41.xml"));                // code 0 = power loss

        var crashes = new EvidenceReader(source).ReadCrashes(LongAgo);

        Assert.Equal(2, crashes.Count);
    }

    [Fact]
    public void Reliability_hang_is_not_a_twin_of_an_app_error()
    {
        var hangRow = new Dictionary<string, string?>(
            System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(Fixture.Text("reliability", "app-failure.json"))!)
        {
            ["SourceName"] = "Application Hang",
            ["EventIdentifier"] = "1002",
            ["TimeGenerated"] = "20261120143020.000000-000",
        };
        var source = new FakeRecordSource().AddAppError(1, T0);
        source.Reliability.Add(new RawReliabilityRecord(hangRow));

        var crashes = new EvidenceReader(source).ReadCrashes(LongAgo);

        Assert.Equal(2, crashes.Count);
    }

    [Fact]
    public void Warnings_added_while_records_are_read_lazily_are_kept()
    {
        var reader = new EvidenceReader(new LazyWarningSource());

        reader.ReadCrashes(LongAgo);

        Assert.Equal(["events warning", "reliability warning"], reader.Warnings);
    }

    private sealed class LazyWarningSource : IRecordSource
    {
        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings => _warnings;

        public IEnumerable<RawEventRecord> ReadEvents(DateTimeOffset since)
        {
            _warnings.Add("events warning");
            yield break;
        }

        public IEnumerable<RawReliabilityRecord> ReadReliability(DateTimeOffset since)
        {
            _warnings.Add("reliability warning");
            yield break;
        }
    }

    [Fact]
    public void Retried_error_reports_count_once()
    {
        // Windows logs a WER 1001 event every time it retries sending a queued report; one report is one problem.
        var xml = Fixture.Text("events", "wer-1001-livekernelevent-141.xml");
        var source = new FakeRecordSource();
        source.Events.Add(new RawEventRecord("Application", xml, null));
        source.Events.Add(new RawEventRecord("Application", xml
            .Replace("<EventRecordID>4245<", "<EventRecordID>4300<")
            .Replace("2026-11-20T14:30:05Z", "2026-11-21T09:00:00Z"), null));
        source.Events.Add(new RawEventRecord("Application", xml
            .Replace("<EventRecordID>4245<", "<EventRecordID>4400<")
            .Replace("11111111-2222-3333-4444-555555555555", "99999999-2222-3333-4444-555555555555"), null));

        var ids = new EvidenceReader(source).ReadCrashes(LongAgo).Select(c => c.Id).Order();

        Assert.Equal(["app-4245", "app-4400"], ids);
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
