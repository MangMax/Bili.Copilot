using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播模式策略：直播永远不进入依赖"时长/终点"的播放路径，视频与 PGC 保持原样。
/// </summary>
public sealed class LiveModePolicyTests
{
    [Fact]
    public void Live_DisablesEveryTimelineCapability()
    {
        var capabilities = LiveModePolicy.For(BiliMediaType.Live);

        Assert.False(capabilities.SupportsTimeline);
        Assert.False(capabilities.ReportsProgress);
        Assert.False(capabilities.SavesHistory);
        Assert.False(capabilities.ResumesPlayback);
        Assert.False(capabilities.DetectsEnd);
        Assert.False(capabilities.AutoPlaysNext);
        Assert.False(capabilities.SupportsChapters);
        Assert.False(capabilities.SupportsSubtitles);
        Assert.False(capabilities.SupportsSpeed);
        Assert.False(capabilities.RunsSponsorBlock);
        Assert.False(capabilities.SupportsDanmakuTimeline);
    }

    [Theory]
    [InlineData(BiliMediaType.Video)]
    [InlineData(BiliMediaType.Pgc)]
    public void NonLive_KeepsEveryTimelineCapability(BiliMediaType type)
    {
        var capabilities = LiveModePolicy.For(type);

        Assert.True(capabilities.SupportsTimeline);
        Assert.True(capabilities.ReportsProgress);
        Assert.True(capabilities.SavesHistory);
        Assert.True(capabilities.ResumesPlayback);
        Assert.True(capabilities.DetectsEnd);
        Assert.True(capabilities.AutoPlaysNext);
        Assert.True(capabilities.SupportsChapters);
        Assert.True(capabilities.SupportsSubtitles);
        Assert.True(capabilities.SupportsSpeed);
        Assert.True(capabilities.RunsSponsorBlock);
        Assert.True(capabilities.SupportsDanmakuTimeline);
    }

    [Theory]
    [InlineData(BiliMediaType.Video, false)]
    [InlineData(BiliMediaType.Pgc, false)]
    [InlineData(BiliMediaType.Live, true)]
    public void IsLive_OnlyMatchesLiveMedia(BiliMediaType type, bool expected)
        => Assert.Equal(expected, LiveModePolicy.IsLive(type));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Capabilities_StayConsistentForEveryDeclaredMediaType(int value)
    {
        var type = (BiliMediaType)value;
        var capabilities = LiveModePolicy.For(type);

        Assert.Equal(type == BiliMediaType.Live, !capabilities.SupportsTimeline);
    }
}
