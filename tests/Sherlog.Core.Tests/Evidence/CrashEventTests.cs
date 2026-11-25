using Sherlog.Core.Evidence;

namespace Sherlog.Core.Tests.Evidence;

public class CrashEventTests
{
    [Theory]
    [InlineData("c0000005", "0xc0000005")]
    [InlineData("0xC0000409", "0xc0000409")]
    [InlineData("0x116", "0x00000116")]
    [InlineData("  e0434352 ", "0xe0434352")]
    public void NormalizeCode_pads_and_prefixes(string raw, string expected)
    {
        Assert.Equal(expected, CrashEvent.NormalizeCode(raw));
    }

    [Fact]
    public void NormalizeCode_converts_decimal_when_asked()
    {
        Assert.Equal("0x00000116", CrashEvent.NormalizeCode("278", fromDecimal: true));
        Assert.Equal("0x00000000", CrashEvent.NormalizeCode("0", fromDecimal: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-code")]
    public void NormalizeCode_returns_null_for_missing_or_garbage(string? raw)
    {
        Assert.Null(CrashEvent.NormalizeCode(raw));
    }

    [Fact]
    public void EvidenceSources_All_lists_every_source_once()
    {
        Assert.Equal(8, EvidenceSources.All.Count);
        Assert.Equal(EvidenceSources.All.Count, EvidenceSources.All.Distinct().Count());
        Assert.Contains("display-reset", EvidenceSources.All);
    }
}
