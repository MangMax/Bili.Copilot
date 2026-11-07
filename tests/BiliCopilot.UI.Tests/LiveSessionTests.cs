using System.Net;
using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 一次直播观看的状态：请求代际、房间信息、播放目标与失败原因。
/// 关闭窗口或换房后，迟到的请求不得再写入会话。
/// </summary>
public sealed class LiveSessionTests
{
    [Fact]
    public void BeginRequest_InvalidatesPreviousGeneration()
    {
        var session = new LiveSession();
        var first = session.BeginRequest();
        var second = session.BeginRequest();

        Assert.True(session.IsCurrent(second.Generation));
        Assert.False(session.IsCurrent(first.Generation));
        Assert.True(first.CancellationToken.IsCancellationRequested);
        Assert.Equal(LiveSessionState.Loading, session.State);
    }

    [Fact]
    public void CancelPending_RejectsLateCompletionAndClosesSession()
    {
        var session = new LiveSession();
        var request = session.BeginRequest();
        session.CancelPending();

        Assert.False(session.IsCurrent(request.Generation));
        Assert.True(request.CancellationToken.IsCancellationRequested);
        Assert.False(session.CompletePage(request.Generation, Page("room", "迟到房间", "主播", isLiving: true)));
        Assert.Null(session.Page);
        Assert.Equal(LiveSessionState.Closed, session.State);
    }

    [Fact]
    public void CompletePage_UpdatesRoomTitleAnchorAndLivingStatus()
    {
        var session = new LiveSession();
        var request = session.BeginRequest();

        Assert.True(session.CompletePage(request.Generation, Page("123", "房间标题", "主播甲", isLiving: true)));

        Assert.Equal("123", session.RoomId);
        Assert.Equal("房间标题", session.Title);
        Assert.Equal("主播甲", session.AnchorName);
        Assert.True(session.IsLiving);
    }

    [Fact]
    public void ApplyTarget_MarksSessionReady()
    {
        var session = new LiveSession();
        var request = session.BeginRequest();
        session.CompletePage(request.Generation, Page("123", "房间标题", "主播甲", isLiving: true));

        var target = new LivePlaybackTarget("https://a.example/live.m3u8", "avc", 400, "http_hls", false);
        Assert.True(session.ApplyTarget(request.Generation, target));

        Assert.Same(target, session.Target);
        Assert.Equal(LiveSessionState.Ready, session.State);
    }

    [Fact]
    public void Fail_RecordsReadableFailureKind()
    {
        var session = new LiveSession();
        var request = session.BeginRequest();

        Assert.True(session.Fail(request.Generation, LiveFailureKind.AuthenticationFailed, "403"));

        Assert.Equal(LiveSessionState.Faulted, session.State);
        Assert.Equal(LiveFailureKind.AuthenticationFailed, session.FailureKind);
        Assert.Equal("403", session.FailureDetail);
    }

    [Fact]
    public void Reset_ClearsPreviousRoomAndFailureState()
    {
        var session = new LiveSession();
        var request = session.BeginRequest();
        session.Fail(request.Generation, LiveFailureKind.AddressUnavailable, "stream gone");

        session.Reset(new LiveInformation(new MediaIdentifier("456", "另一个房间", null)));

        Assert.Equal("456", session.RoomId);
        Assert.Equal("另一个房间", session.Title);
        Assert.Null(session.Page);
        Assert.Null(session.Target);
        Assert.Equal(LiveSessionState.Idle, session.State);
        Assert.Equal(LiveFailureKind.None, session.FailureKind);
        Assert.Null(session.FailureDetail);
    }

    [Fact]
    public void BrowserFallback_IsTerminalForNativeAttempt()
    {
        var session = new LiveSession();
        var request = session.BeginRequest();

        Assert.True(session.MarkBrowserFallback(request.Generation));

        Assert.Equal(LiveSessionState.BrowserFallback, session.State);
    }

    [Fact]
    public void StaleFailure_DoesNotOverwriteCurrentState()
    {
        var session = new LiveSession();
        var stale = session.BeginRequest();
        var current = session.BeginRequest();

        Assert.False(session.Fail(stale.Generation, LiveFailureKind.AddressUnavailable, "old"));
        Assert.Equal(LiveSessionState.Loading, session.State);
        Assert.Equal(LiveFailureKind.None, session.FailureKind);
        Assert.True(session.IsCurrent(current.Generation));
    }

    [Fact]
    public void Classify_MapsTransportFailuresToDistinctKinds()
    {
        Assert.Equal(LiveFailureKind.None, LiveFailureClassifier.Classify(null));
        Assert.Equal(LiveFailureKind.Cancelled, LiveFailureClassifier.Classify(new OperationCanceledException()));
        Assert.Equal(LiveFailureKind.AuthenticationFailed, LiveFailureClassifier.Classify(new HttpRequestException("403", null, HttpStatusCode.Forbidden)));
        Assert.Equal(LiveFailureKind.AuthenticationFailed, LiveFailureClassifier.Classify(new HttpRequestException("401", null, HttpStatusCode.Unauthorized)));
        Assert.Equal(LiveFailureKind.AddressUnavailable, LiveFailureClassifier.Classify(new HttpRequestException("network")));
        Assert.Equal(LiveFailureKind.AddressUnavailable, LiveFailureClassifier.Classify(new IOException("reset")));
        Assert.Equal(LiveFailureKind.Unknown, LiveFailureClassifier.Classify(new InvalidOperationException("boom")));
    }

    private static LivePlayerView Page(string roomId, string title, string anchor, bool isLiving)
    {
        var info = new LiveInformation(
            new MediaIdentifier(roomId, title, null),
            new UserProfile("anchor", anchor, null));
        info.AddExtensionIfNotNull(LiveExtensionDataId.IsLiving, isLiving);
        return new LivePlayerView(info, new LiveTag("area", "分区", "default"));
    }
}
