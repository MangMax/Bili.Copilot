// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class UpdateMirrorPolicyTests
{
    private const long Hour = TimeSpan.TicksPerHour;

    [Fact]
    public void ParseUserPrefixes_SplitsLinesAndTrims()
    {
        var prefixes = UpdateMirrorPolicy.ParseUserPrefixes(
            "https://a.example/\r\n  https://b.example  \n");

        Assert.Equal(["https://a.example/", "https://b.example"], prefixes);
    }

    [Fact]
    public void ParseUserPrefixes_IgnoresNonHttps()
        => Assert.Empty(UpdateMirrorPolicy.ParseUserPrefixes(
            "http://insecure.example\nftp://x.example\nghproxy.example\nhttps://"));

    [Fact]
    public void ParseUserPrefixes_IgnoresOverlongLines()
    {
        var tooLong = "https://example.com/" + new string('a', UpdateMirrorPolicy.MaxPrefixLength);
        Assert.Empty(UpdateMirrorPolicy.ParseUserPrefixes(tooLong));
    }

    [Fact]
    public void ParseUserPrefixes_WithBlankInput_ReturnsEmpty()
    {
        Assert.Empty(UpdateMirrorPolicy.ParseUserPrefixes(null));
        Assert.Empty(UpdateMirrorPolicy.ParseUserPrefixes("   "));
        Assert.Empty(UpdateMirrorPolicy.ParseUserPrefixes(string.Empty));
    }

    [Fact]
    public void IsSourceFresh_WithinWindow_IsFresh()
        => Assert.True(UpdateMirrorPolicy.IsSourceFresh(
            recordedTicks: 1000,
            nowTicks: 1000 + Hour,
            maxAgeTicks: 12 * Hour));

    [Fact]
    public void IsSourceFresh_AfterWindow_IsStale()
        => Assert.False(UpdateMirrorPolicy.IsSourceFresh(
            recordedTicks: 1000,
            nowTicks: 1000 + (13 * Hour),
            maxAgeTicks: 12 * Hour));

    [Fact]
    public void IsSourceFresh_WithoutRecord_IsStale()
        => Assert.False(UpdateMirrorPolicy.IsSourceFresh(0, 1000 + Hour, 12 * Hour));

    [Fact]
    public void IsSourceFresh_WithClockGoingBackwards_IsStale()
        => Assert.False(UpdateMirrorPolicy.IsSourceFresh(2000, 1000, 12 * Hour));
}
