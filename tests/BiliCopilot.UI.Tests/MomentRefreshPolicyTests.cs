// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

public sealed class MomentRefreshPolicyTests
{
    [Fact]
    public void ExtractLatestIdsSeparatesVideoAndNonVideoMoments()
    {
        var view = new MomentView(
            [
                Moment("205", MomentItemType.Image),
                Moment("204", MomentItemType.Video),
                Moment("203", MomentItemType.Pgc),
                Moment("202", MomentItemType.Forward),
                Moment("invalid", MomentItemType.Video),
            ],
            null,
            null,
            null,
            false);

        var (latestAll, latestVideo, latestNonVideo) = MomentRefreshPolicy.ExtractLatestIds(view);

        Assert.Equal(205UL, latestAll);
        Assert.Equal(204UL, latestVideo);
        Assert.Equal(205UL, latestNonVideo);
    }

    [Fact]
    public void IsVideoMomentRecognizesVideoPgcAndUntypedMediaFallback()
    {
        var videoData = new VideoInformation(new MediaIdentifier("1", "title", default!), default!);
        var episodeData = new EpisodeInformation(new MediaIdentifier("2", "ep", default!));

        Assert.True(MomentRefreshPolicy.IsVideoMoment(Moment("10", MomentItemType.Video)));
        Assert.True(MomentRefreshPolicy.IsVideoMoment(Moment("11", MomentItemType.Pgc)));
        Assert.True(MomentRefreshPolicy.IsVideoMoment(Moment("12", null, videoData)));
        Assert.True(MomentRefreshPolicy.IsVideoMoment(Moment("13", null, episodeData)));
        Assert.False(MomentRefreshPolicy.IsVideoMoment(Moment("14", MomentItemType.Forward, videoData)));
        Assert.False(MomentRefreshPolicy.IsVideoMoment(Moment("15", MomentItemType.PlainText)));
    }

    [Fact]
    public void ColdStartWithLatestVideoAlreadyLoadedHasNoUnreadMoments()
        => Assert.False(MomentRefreshPolicy.HasUnreadMoments(
            seenMomentId: 100, loadedVideoMomentId: 105, latestVideoMomentId: 105, latestNonVideoMomentId: 99));

    [Fact]
    public void VideoLoadedOnColdStartDoesNotHideUnseenNonVideoMoments()
    {
        Assert.True(MomentRefreshPolicy.HasUnreadMoments(
            seenMomentId: 100, loadedVideoMomentId: 108, latestVideoMomentId: 108, latestNonVideoMomentId: 105));
        Assert.False(MomentRefreshPolicy.ShouldRefreshVideoSection(
            isCurrentlyVideoSection: true, seenMomentId: 100, latestVideoMomentId: 108,
            latestNonVideoMomentId: 105, loadedVideoMomentId: 108));
    }

    [Fact]
    public void ANewerVideoStillTriggersThePromptAfterInitialLoad()
        => Assert.True(MomentRefreshPolicy.HasUnreadMoments(
            seenMomentId: 100, loadedVideoMomentId: 105, latestVideoMomentId: 110, latestNonVideoMomentId: 99));

    [Fact]
    public void VideoTabStaysOnVideoWhenNewVideoArrives()
    {
        Assert.True(MomentRefreshPolicy.ShouldRefreshVideoSection(
            isCurrentlyVideoSection: true,
            seenMomentId: 100,
            latestVideoMomentId: 105,
            latestNonVideoMomentId: 90));

        // 即使同时存在其他非视频动态，当前处于视频页且有新视频时仍留在视频页刷新。
        Assert.True(MomentRefreshPolicy.ShouldRefreshVideoSection(
            isCurrentlyVideoSection: true,
            seenMomentId: 100,
            latestVideoMomentId: 105,
            latestNonVideoMomentId: 108));
    }

    [Fact]
    public void VideoTabSwitchesToComprehensiveOnlyWhenUpdatesAreNonVideoOnly()
    {
        Assert.False(MomentRefreshPolicy.ShouldRefreshVideoSection(
            isCurrentlyVideoSection: true,
            seenMomentId: 100,
            latestVideoMomentId: 100,
            latestNonVideoMomentId: 108));
    }

    [Fact]
    public void ComprehensiveTabAlwaysStaysOnComprehensive()
    {
        Assert.False(MomentRefreshPolicy.ShouldRefreshVideoSection(
            isCurrentlyVideoSection: false,
            seenMomentId: 100,
            latestVideoMomentId: 105,
            latestNonVideoMomentId: 90));
    }

    [Fact]
    public void ResolveSeenAfterVideoRefreshClearsCheckedBatchWithoutHidingNewerArrivals()
    {
        // 拉到最新视频后清掉本轮提示（包含同批更晚的非视频动态 108），但若刷新期间又有 120 到来仍大于 seen。
        var seen = MomentRefreshPolicy.ResolveSeenAfterRefresh(
            refreshedVideoSection: true,
            previousSeen: 100,
            refreshedLatestId: 105,
            checkedLatestAll: 108,
            checkedLatestVideo: 105);

        Assert.Equal(108UL, seen);
        Assert.True(120UL > seen);
    }

    [Fact]
    public void ResolveSeenAfterVideoRefreshKeepsPromptWhenVideoFeedHasNotCaughtUp()
    {
        var seen = MomentRefreshPolicy.ResolveSeenAfterRefresh(
            refreshedVideoSection: true,
            previousSeen: 100,
            refreshedLatestId: 102,
            checkedLatestAll: 105,
            checkedLatestVideo: 105);

        Assert.Equal(102UL, seen);
        Assert.True(105UL > seen);
    }

    private static MomentInformation Moment(string id, MomentItemType? type, object? data = null)
        => new(id, new UserProfile("1", "UP", default!), dynamicType: type, data: data);
}
