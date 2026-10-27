// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockColorParserTests
{
    [Theory]
    [InlineData(0xFF000000u, "#FF000000")]
    [InlineData(0xFFD88CA8u, "#FFD88CA8")]
    [InlineData(0x00123456u, "#00123456")]
    [InlineData(0xFFFFFFFFu, "#FFFFFFFF")]
    public void Format_UsesHashAndEightHexDigits(uint argb, string expected)
        => Assert.Equal(expected, SponsorBlockColorParser.Format(argb));

    [Theory]
    [InlineData("#FF000000", 0xFF000000u)]
    [InlineData("#FFD88CA8", 0xFFD88CA8u)]
    [InlineData("#ffd88ca8", 0xFFD88CA8u)]
    [InlineData("  #00123456  ", 0x00123456u)]
    public void TryParse_AcceptsEightDigitArgb(string value, uint expected)
    {
        Assert.True(SponsorBlockColorParser.TryParse(value, out var argb));
        Assert.Equal(expected, argb);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("FFD88CA8")]
    [InlineData("0xFFD88CA8")]
    [InlineData("#FFD88CA")]
    [InlineData("#FFD88CA80")]
    [InlineData("#GGD88CA8")]
    [InlineData("##FD88CA8")]
    [InlineData("# FFD88CA8")]
    public void TryParse_RejectsMalformedValues(string? value)
    {
        Assert.False(SponsorBlockColorParser.TryParse(value, out var argb));
        Assert.Equal(0u, argb);
    }

    [Theory]
    [InlineData(0xFF000000u)]
    [InlineData(0xFFD88CA8u)]
    [InlineData(0x7F3366CCu)]
    [InlineData(0x00123456u)]
    public void FormatThenParse_RoundTrips(uint argb)
    {
        var text = SponsorBlockColorParser.Format(argb);

        Assert.True(SponsorBlockColorParser.TryParse(text, out var parsed));
        Assert.Equal(argb, parsed);
    }
}
