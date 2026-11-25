using System.Text.Json;
using Sherlog.Core.Evidence;

namespace Sherlog.Core.Tests.Evidence;

public class ReliabilityRecordParserTests
{
    private static RawReliabilityRecord Load(string file) =>
        new(JsonSerializer.Deserialize<Dictionary<string, string?>>(Fixture.Text("reliability", file))!);

    [Fact]
    public void Reliability_app_failure_maps_product_module_code_and_time()
    {
        var crash = ReliabilityRecordParser.Parse(Load("app-failure.json"));

        Assert.NotNull(crash);
        Assert.Equal("rel-901", crash.Id);
        Assert.Equal(EvidenceSources.Reliability, crash.Source);
        Assert.Equal(1000, crash.EventId);
        Assert.Equal("Application Error", crash.Provider);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 14, 30, 0, TimeSpan.Zero), crash.Time);
        Assert.Equal("SherlogTestGame.exe", crash.AppName);
        Assert.Equal("FakeOverlay64.dll", crash.FaultingModule);
        Assert.Equal("0xc0000005", crash.ExceptionCode);
    }

    [Fact]
    public void Reliability_bluescreen_maps_bugcheck_code_without_app()
    {
        var crash = ReliabilityRecordParser.Parse(Load("windows-failure.json"));

        Assert.NotNull(crash);
        Assert.Equal("rel-902", crash.Id);
        Assert.Equal("0x00000116", crash.BugCheckCode);
        Assert.Null(crash.AppName);
    }

    [Fact]
    public void Reliability_unrelated_record_returns_null()
    {
        Assert.Null(ReliabilityRecordParser.Parse(Load("unrelated.json")));
    }

    [Fact]
    public void Reliability_time_with_offset_is_converted_to_utc()
    {
        var props = new Dictionary<string, string?>(Load("app-failure.json").Properties)
        {
            ["TimeGenerated"] = "20261120173000.000000+180",
        };

        var crash = ReliabilityRecordParser.Parse(new RawReliabilityRecord(props));

        Assert.Equal(new DateTimeOffset(2026, 11, 20, 14, 30, 0, TimeSpan.Zero), crash?.Time);
    }
}
