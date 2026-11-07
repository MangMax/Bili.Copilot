// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Bili.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 播放不可用原因到界面提示与操作的映射.
/// </summary>
public class PlaybackUnavailableClassifierTests
{
    [Theory]
    [InlineData(PgcPlaybackUnavailableReason.LoginRequired, false, PlaybackUnavailableMessage.LoginRequired, PlaybackUnavailableAction.ReSignIn)]
    [InlineData(PgcPlaybackUnavailableReason.NotEntitled, false, PlaybackUnavailableMessage.NeedVip, PlaybackUnavailableAction.OpenVipPage)]
    [InlineData(PgcPlaybackUnavailableReason.NotEntitled, true, PlaybackUnavailableMessage.SessionExpired, PlaybackUnavailableAction.ReSignIn)]
    [InlineData(PgcPlaybackUnavailableReason.RegionRestricted, true, PlaybackUnavailableMessage.RegionRestricted, PlaybackUnavailableAction.None)]
    [InlineData(PgcPlaybackUnavailableReason.ContentUnavailable, true, PlaybackUnavailableMessage.ContentUnavailable, PlaybackUnavailableAction.None)]
    public void Classify_MapsKernelReason(
        PgcPlaybackUnavailableReason reason,
        bool isVip,
        PlaybackUnavailableMessage expectedMessage,
        PlaybackUnavailableAction expectedAction)
    {
        var notice = PlaybackUnavailableClassifier.Classify(reason, isVip);

        Assert.Equal(expectedMessage, notice.Message);
        Assert.Equal(expectedAction, notice.Action);
    }

    [Fact]
    public void Classify_UnknownFailureReportsSourceFailureWithoutAction()
    {
        var notice = PlaybackUnavailableClassifier.Classify(null, isVip: true);

        Assert.Equal(PlaybackUnavailableMessage.SourceFailed, notice.Message);
        Assert.Equal(PlaybackUnavailableAction.None, notice.Action);
    }
}
