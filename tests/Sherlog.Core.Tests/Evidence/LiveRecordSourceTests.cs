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
}
