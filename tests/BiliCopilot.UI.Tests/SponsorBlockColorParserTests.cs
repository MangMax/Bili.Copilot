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
    [InlineData(0x7F3366CCu)]
    [InlineData(0x00123456u)]
    [InlineData(0x80FFFFFFu)]
    public void GetDisplayString_KeepsAlphaWhenTranslucent(uint argb)
        => Assert.Equal(SponsorBlockColorParser.Format(argb), SponsorBlockColorParser.ToDisplayString(argb));

    [Theory]
    [InlineData(0xFF000000u, "#000000")]
    [InlineData(0xFFD88CA8u, "#D88CA8")]
    [InlineData(0xFFFFFFFFu, "#FFFFFF")]
    public void GetDisplayString_DropsOpaqueAlphaPrefix(uint argb, string expected)
        => Assert.Equal(expected, SponsorBlockColorParser.ToDisplayString(argb));

    [Fact]
    public void GetDisplayString_OpaqueAndTranslucent_AreDistinguishable()
    {
        // 同一组 RGB 下，带透明度时文本必须与不透明时不同，否则用户看不出自己调过透明度.
        Assert.NotEqual(
            SponsorBlockColorParser.ToDisplayString(0xFF3366CCu),
            SponsorBlockColorParser.ToDisplayString(0x803366CCu));
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
