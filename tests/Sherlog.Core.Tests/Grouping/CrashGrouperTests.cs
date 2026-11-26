using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;

namespace Sherlog.Core.Tests.Grouping;

public class CrashGrouperTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 14, 30, 0, TimeSpan.Zero);

    private static CrashEvent AppCrash(string id, DateTimeOffset time, string app = "SherlogTestGame.exe",
        string module = "FakeOverlay64.dll", string code = "0xc0000005", string source = EvidenceSources.ApplicationError) =>
        new(id, time, source, "Application Error", 1000, app, "1.0", module, "1.0", code, null, null, null);

    [Fact]
    public void Same_app_module_code_form_one_group()
    {
        var groups = CrashGrouper.Group([AppCrash("app-1", T0), AppCrash("app-2", T0.AddDays(1))]);

        var group = Assert.Single(groups);
        Assert.Equal(["app-2", "app-1"], group.Crashes.Select(c => c.Id));
        Assert.Equal("app-2", group.Latest.Id);
    }

    [Fact]
    public void Different_exception_code_splits_groups()
    {
        var groups = CrashGrouper.Group([AppCrash("app-1", T0), AppCrash("app-2", T0, code: "0xc0000409")]);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void Names_are_compared_case_insensitively()
    {
        var groups = CrashGrouper.Group([AppCrash("app-1", T0), AppCrash("app-2", T0, app: "SHERLOGTESTGAME.EXE")]);

        Assert.Single(groups);
    }

    [Fact]
    public void Reliability_and_app_error_for_same_crash_share_a_group()
    {
        var groups = CrashGrouper.Group(
        [
            AppCrash("app-1", T0),
            AppCrash("rel-9", T0.AddDays(-3), source: EvidenceSources.Reliability),
        ]);

        Assert.Single(groups);
    }

    [Fact]
    public void Group_id_is_stable()
    {
        var group = Assert.Single(CrashGrouper.Group([AppCrash("app-1", T0)]));

        Assert.Equal("app|sherlogtestgame.exe|fakeoverlay64.dll|0xc0000005", group.Signature);
        Assert.Equal("g-74537a", group.Id);
    }

    [Fact]
    public void Driver_resets_group_by_driver()
    {
        static CrashEvent Reset(string id, string driver) =>
            new(id, T0, EvidenceSources.DisplayReset, "Display", 4101, null, null, driver, null, null, null, null, null);

        var groups = CrashGrouper.Group([Reset("sys-1", "nvlddmkm"), Reset("sys-2", "nvlddmkm"), Reset("sys-3", "amdkmdag")]);

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Signature == "display|nvlddmkm");
    }

    [Fact]
    public void Blue_screens_group_by_code_across_sources()
    {
        var fromLog = new CrashEvent("sys-1", T0, EvidenceSources.BugCheck, "x", 1001, null, null, null, null, null, "0x00000116", null, null);
        var fromReliability = fromLog with { Id = "rel-1", Source = EvidenceSources.Reliability, Time = T0.AddDays(-5) };

        var group = Assert.Single(CrashGrouper.Group([fromLog, fromReliability]));

        Assert.Equal("bugcheck|0x00000116", group.Signature);
    }

    [Fact]
    public void Missing_dll_popups_group_by_app_and_dll()
    {
        var popup = new CrashEvent("sys-1", T0, EvidenceSources.AppPopup, "Application Popup", 26,
            "Game.exe", null, "VCRUNTIME140.dll", null, null, null, null, null);

        var group = Assert.Single(CrashGrouper.Group([popup, popup with { Id = "sys-2" }]));

        Assert.Equal("popup|game.exe|vcruntime140.dll", group.Signature);
    }

    [Fact]
    public void Groups_sorted_by_latest_crash()
    {
        var groups = CrashGrouper.Group(
        [
            AppCrash("app-1", T0, app: "Old.exe"),
            AppCrash("app-2", T0.AddDays(2), app: "New.exe"),
            AppCrash("app-3", T0.AddDays(1), app: "Middle.exe"),
        ]);

        Assert.Equal(["app-2", "app-3", "app-1"], groups.Select(g => g.Latest.Id));
    }
}
