using Sherlog.Core.Evidence;

namespace Sherlog.Core.Tests.Evidence;

public class LiveRecordSourceTests
{
    // Reads this PC's real logs. It must never print or assert on record content (privacy rule in plan.md).
    [Fact]
    public void Reads_this_pc_without_throwing()
    {
        var source = new LiveRecordSource();
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        var eventCount = source.ReadEvents(since).Count();
        var reliabilityCount = source.ReadReliability(since).Count();

        Assert.True(eventCount >= 0);
        Assert.True(reliabilityCount >= 0);
        // Both logs and Reliability Monitor are readable without admin rights, so a warning means a broken query.
        Assert.Empty(source.Warnings);
    }

    [Fact]
    public void Query_filters_to_crash_providers_and_time()
    {
        var since = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var query = LiveRecordSource.BuildQuery("System", since);

        Assert.Contains("Provider[@Name='Display'] and EventID=4101", query);
        Assert.Contains("Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41", query);
        Assert.Contains("@SystemTime>='2026-11-01T00:00:00.000Z'", query);
        Assert.DoesNotContain("Application Error", query);
    }

    [Fact]
    public void Application_query_asks_only_for_live_kernel_error_reports()
    {
        var query = LiveRecordSource.BuildQuery("Application", new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        // EventData must be a sibling of System inside the same *[...]; "*[EventData…]" nested there matches nothing.
        Assert.Contains("Provider[@Name='Windows Error Reporting'] and EventID=1001", query);
        Assert.Contains("]] and EventData[Data[@Name='EventName']='LiveKernelEvent']]", query);
        Assert.DoesNotContain("*[EventData", query);
    }

    // Reads this PC's real log. Prints nothing; compares counts only.
    // Windows logs a report again on every retry; the source returns each report once.
    [Fact]
    public void Live_kernel_reports_are_read_once_each()
    {
        var since = DateTimeOffset.UtcNow.AddDays(-30);
        var time = since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var unfiltered = $"*[System[Provider[@Name='Windows Error Reporting'] and EventID=1001 and TimeCreated[@SystemTime>='{time}']]]";

        var expectedReports = DistinctLiveKernelReports(unfiltered);
        var returned = new LiveRecordSource().ReadEvents(since)
            .Select(ApplicationEventParser.Parse)
            .Where(c => c?.EventName == "LiveKernelEvent")
            .Select(c => c!.ReportId ?? c.Id)
            .ToList();

        Assert.Equal(expectedReports, returned.Count);
        Assert.Equal(returned.Count, returned.Distinct().Count());
    }

    private static int DistinctLiveKernelReports(string xpath)
    {
        using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(
            new System.Diagnostics.Eventing.Reader.EventLogQuery("Application", System.Diagnostics.Eventing.Reader.PathType.LogName, xpath));
        var reports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var e = reader.ReadEvent(); e is not null; e = reader.ReadEvent())
        {
            using (e)
            {
                var crash = ApplicationEventParser.Parse(new RawEventRecord("Application", e.ToXml(), null));
                if (crash?.EventName == "LiveKernelEvent")
                {
                    reports.Add(crash.ReportId ?? crash.Id);
                }
            }
        }

        return reports.Count;
    }
}
