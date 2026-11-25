using Sherlog.Core.Evidence;

namespace Sherlog.Core.Tests.Evidence;

public class ApplicationEventParserTests
{
    [Fact]
    public void AppError_named_data_maps_all_fields()
    {
        var crash = ApplicationEventParser.Parse(Fixture.Event("Application", "app-error-1000-named.xml"));

        Assert.NotNull(crash);
        Assert.Equal("app-4242", crash.Id);
        Assert.Equal(EvidenceSources.ApplicationError, crash.Source);
        Assert.Equal("Application Error", crash.Provider);
        Assert.Equal(1000, crash.EventId);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 14, 30, 0, TimeSpan.Zero), crash.Time.AddTicks(-1234567));
        Assert.Equal(TimeSpan.Zero, crash.Time.Offset);
        Assert.Equal("SherlogTestGame.exe", crash.AppName);
        Assert.Equal("1.2.3.4", crash.AppVersion);
        Assert.Equal("FakeOverlay64.dll", crash.FaultingModule);
        Assert.Equal("5.6.7.8", crash.ModuleVersion);
        Assert.Equal("0xc0000005", crash.ExceptionCode);
        Assert.Null(crash.BugCheckCode);
    }

    [Fact]
    public void AppError_positional_data_maps_same_fields()
    {
        var crash = ApplicationEventParser.Parse(Fixture.Event("Application", "app-error-1000-positional.xml"));

        Assert.NotNull(crash);
        Assert.Equal("app-4243", crash.Id);
        Assert.Equal("SherlogTestGame.exe", crash.AppName);
        Assert.Equal("1.2.3.4", crash.AppVersion);
        Assert.Equal("FakeOverlay64.dll", crash.FaultingModule);
        Assert.Equal("5.6.7.8", crash.ModuleVersion);
        Assert.Equal("0xc0000005", crash.ExceptionCode);
    }

    [Fact]
    public void AppHang_maps_app_name_and_version()
    {
        var crash = ApplicationEventParser.Parse(Fixture.Event("Application", "app-hang-1002.xml"));

        Assert.NotNull(crash);
        Assert.Equal("app-5001", crash.Id);
        Assert.Equal(EvidenceSources.ApplicationHang, crash.Source);
        Assert.Equal(1002, crash.EventId);
        Assert.Equal("SherlogTestGame.exe", crash.AppName);
        Assert.Equal("1.2.3.4", crash.AppVersion);
        Assert.Null(crash.FaultingModule);
        Assert.Null(crash.ExceptionCode);
    }

    [Fact]
    public void Wer_appcrash_is_ignored_because_event_1000_already_has_it()
    {
        Assert.Null(ApplicationEventParser.Parse(Fixture.Event("Application", "wer-1001-appcrash.xml")));
    }

    [Fact]
    public void Wer_livekernelevent_maps_event_name_and_code()
    {
        var crash = ApplicationEventParser.Parse(Fixture.Event("Application", "wer-1001-livekernelevent-141.xml"));

        Assert.NotNull(crash);
        Assert.Equal("app-4245", crash.Id);
        Assert.Equal(EvidenceSources.ErrorReport, crash.Source);
        Assert.Equal("LiveKernelEvent", crash.EventName);
        Assert.Equal("0x00000141", crash.BugCheckCode);
    }

    [Theory]
    [InlineData("WindowsUpdateFailure3")]
    [InlineData("AppHangB1")]
    [InlineData("BEX64")]
    public void Wer_reports_other_than_kernel_and_bluescreen_are_ignored(string eventName)
    {
        var xml = Fixture.Text("events", "wer-1001-livekernelevent-141.xml").Replace("LiveKernelEvent", eventName);

        Assert.Null(ApplicationEventParser.Parse(new RawEventRecord("Application", xml, null)));
    }

    [Fact]
    public void Unrelated_event_returns_null()
    {
        Assert.Null(ApplicationEventParser.Parse(Fixture.Event("Application", "unrelated-app-event.xml")));
    }

    [Fact]
    public void Message_is_carried_through()
    {
        var crash = ApplicationEventParser.Parse(
            Fixture.Event("Application", "app-error-1000-named.xml", message: "Faulting application name: x"));

        Assert.Equal("Faulting application name: x", crash?.Message);
    }

    [Theory]
    [InlineData(@"C:\Games\Thing\game.exe", "game.exe")]
    [InlineData("game.exe", "game.exe")]
    [InlineData("  ", null)]
    public void FileNameOnly_strips_paths(string input, string? expected)
    {
        Assert.Equal(expected, CrashEvent.FileNameOnly(input));
    }
}
