using Sherlog.Core.Evidence;

namespace Sherlog.Core.Tests.Evidence;

public class SystemEventParserTests
{
    [Fact]
    public void DisplayReset_driver_name_comes_from_data_not_message()
    {
        // A German Windows renders a localized message; the driver name must come from the event data.
        const string german = "Der Anzeigetreiber xyz reagiert nicht mehr und wurde wiederhergestellt.";

        var crash = SystemEventParser.Parse(Fixture.Event("System", "display-4101.xml", german));

        Assert.NotNull(crash);
        Assert.Equal("sys-7001", crash.Id);
        Assert.Equal(EvidenceSources.DisplayReset, crash.Source);
        Assert.Equal(4101, crash.EventId);
        Assert.Equal("nvlddmkm", crash.FaultingModule);
        Assert.Equal(german, crash.Message);
    }

    [Fact]
    public void BugCheck_extracts_code_from_param1()
    {
        var crash = SystemEventParser.Parse(Fixture.Event("System", "bugcheck-1001.xml"));

        Assert.NotNull(crash);
        Assert.Equal("sys-7002", crash.Id);
        Assert.Equal(EvidenceSources.BugCheck, crash.Source);
        Assert.Equal("0x00000116", crash.BugCheckCode);
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero), crash.Time);
    }

    [Fact]
    public void KernelPower_zero_bugcheck_means_power_loss()
    {
        var crash = SystemEventParser.Parse(Fixture.Event("System", "kernel-power-41.xml"));

        Assert.NotNull(crash);
        Assert.Equal(EvidenceSources.UnexpectedShutdown, crash.Source);
        Assert.Equal("0x00000000", crash.BugCheckCode);
    }

    [Fact]
    public void KernelPower_decimal_bugcheck_is_normalized()
    {
        var crash = SystemEventParser.Parse(Fixture.Event("System", "kernel-power-41-bugcheck.xml"));

        Assert.Equal("0x00000116", crash?.BugCheckCode);
    }

    [Fact]
    public void Application_events_are_not_system_crashes()
    {
        Assert.Null(SystemEventParser.Parse(Fixture.Event("Application", "app-error-1000-named.xml")));
    }
}
