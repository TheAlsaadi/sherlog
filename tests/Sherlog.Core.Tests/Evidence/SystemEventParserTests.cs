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
    public void Missing_dll_popup_maps_app_and_dll_even_with_a_translated_caption()
    {
        var crash = SystemEventParser.Parse(Fixture.Event("System", "app-popup-26-missing-dll.xml"));

        Assert.NotNull(crash);
        Assert.Equal("sys-386", crash.Id);
        Assert.Equal(EvidenceSources.AppPopup, crash.Source);
        Assert.Equal(26, crash.EventId);
        Assert.Equal("SherlogTestGame.exe", crash.AppName);
        Assert.Equal("VCRUNTIME140.dll", crash.FaultingModule);
    }

    private static RawEventRecord Popup(string caption, string message) => new("System",
        Fixture.Text("events", "app-popup-26-missing-dll.xml")
            .Replace(Fixture.Text("events", "app-popup-26-missing-dll.xml").Split("<Data Name=\"Caption\">")[1].Split("</Data>")[0], caption)
            .Replace("The code execution cannot proceed because VCRUNTIME140.dll was not found. Reinstalling the program may fix this problem. ", message),
        null);

    [Theory]
    [InlineData("Game.exe - Entry Point Not Found", @"The procedure entry point ?x@@YAXXZ could not be located in the dynamic link library C:\WINDOWS\SYSTEM32\MSVCP140.dll.")]
    [InlineData("Game.exe - Bad Image", @"C:\Games\x.dll is either not designed to run on Windows or it contains an error. Error status 0xc000012f.")]
    [InlineData("Game.exe - Ordinal Not Found", @"The ordinal 42 could not be located in the dynamic link library C:\Games\helper.dll.")]
    public void Popups_about_a_dll_that_is_present_are_not_missing_dll_crashes(string caption, string message)
    {
        Assert.Null(SystemEventParser.Parse(Popup(caption, message)));
    }

    [Fact]
    public void Caption_with_leading_right_to_left_mark_still_names_the_app()
    {
        var crash = SystemEventParser.Parse(Popup("\u200F\u202BGame.exe - \u0628", "The code execution cannot proceed because MSVCP140.dll was not found."));

        Assert.Equal("Game.exe", crash?.AppName);
        Assert.Equal("MSVCP140.dll", crash?.FaultingModule);
    }

    [Fact]
    public void Popup_that_names_no_app_and_dll_is_not_a_crash()
    {
        Assert.Null(SystemEventParser.Parse(Fixture.Event("System", "app-popup-26-other.xml")));
    }

    [Fact]
    public void Application_events_are_not_system_crashes()
    {
        Assert.Null(SystemEventParser.Parse(Fixture.Event("Application", "app-error-1000-named.xml")));
    }
}
