using System.Net;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.PlaybackRouting;
using Richasy.MpvKernel;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播播放失败分类与恢复次序（#194）：
/// 网络/HTTP/解码失败换候选，候选耗尽重新取址，仍失败交回手动重试；
/// 内核在 HTTP 错误后无条件补发的 VoInitFailed 不得重复触发降级。
/// </summary>
public sealed class LiveFailureRecoveryTests
{
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData((HttpStatusCode)412)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void ClassifyPlayback_MapsHttpStatusesToTransport(HttpStatusCode status)
    {
        var exception = new HttpRequestException("failed", null, status);

        Assert.Equal(LivePlaybackFailureKind.Transport, LivePlaybackFailureClassifier.ClassifyPlayback(exception));
    }

    [Fact]
    public void ClassifyPlayback_MapsConnectionAndTimeoutToTransport()
    {
        Assert.Equal(LivePlaybackFailureKind.Transport, LivePlaybackFailureClassifier.ClassifyPlayback(new HttpRequestException("dns")));
        Assert.Equal(LivePlaybackFailureKind.Transport, LivePlaybackFailureClassifier.ClassifyPlayback(new System.Net.Sockets.SocketException()));
        Assert.Equal(LivePlaybackFailureKind.Transport, LivePlaybackFailureClassifier.ClassifyPlayback(new IOException()));
        Assert.Equal(LivePlaybackFailureKind.Transport, LivePlaybackFailureClassifier.ClassifyPlayback(new TimeoutException()));
        Assert.Equal(LivePlaybackFailureKind.Transport, LivePlaybackFailureClassifier.ClassifyPlayback(new TaskCanceledException("timeout", new TimeoutException())));
    }

    [Fact]
    public void ClassifyPlayback_MapsUserCancellationToCancelled()
    {
        Assert.Equal(LivePlaybackFailureKind.Cancelled, LivePlaybackFailureClassifier.ClassifyPlayback(new OperationCanceledException()));
        Assert.Equal(LivePlaybackFailureKind.None, LivePlaybackFailureClassifier.ClassifyPlayback(null));
    }

    [Theory]
    [InlineData(MpvError.VoInitFailed)]
    [InlineData(MpvError.AoInitFailed)]
    [InlineData(MpvError.Unsupported)]
    [InlineData(MpvError.UnknownFormat)]
    [InlineData(MpvError.NothingToPlay)]
    public void FromPlayerError_MapsDecoderFailuresToDecoderInit(MpvError error)
    {
        Assert.Equal(LivePlaybackFailureKind.DecoderInit, LiveNativePlaybackFailure.Classify(error));
    }

    [Theory]
    [InlineData(MpvError.Http403)]
    [InlineData(MpvError.Http404)]
    [InlineData(MpvError.Http500)]
    [InlineData(MpvError.TlsError)]
    [InlineData(MpvError.LoadingFailed)]
    public void FromPlayerError_MapsTransportErrorsToTransport(MpvError error)
    {
        Assert.Equal(LivePlaybackFailureKind.Transport, LiveNativePlaybackFailure.Classify(error));
    }

    [Fact]
    public void FromPlayerError_Http412AdvancesToNextCandidateInsteadOfStalling()
    {
        // 回归：412 以前落到 Unknown，界面只能手动重试；现在按传输失败换候选。
        var sequencer = new LiveRecoverySequencer();

        Assert.Equal(
            LiveRecoveryAction.AdvanceCandidate,
            sequencer.Report(LivePlaybackFailureClassifier.ClassifyPlayback(new HttpRequestException("expired", null, (HttpStatusCode)412)), hasNextCandidate: true));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData((HttpStatusCode)412)]
    public void Classify_MapsPreconditionAndAuthStatusesToAuthenticationFailed(HttpStatusCode status)
    {
        // 取址阶段：412 与 401/403 一样代表登录态/清晰度权限被拒绝。
        var exception = new HttpRequestException("failed", null, status);

        Assert.Equal(LiveFailureKind.AuthenticationFailed, LiveFailureClassifier.Classify(exception));
    }

    [Fact]
    public void Sequencer_AdvancesCandidateWhenOneRemains()
    {
        var sequencer = new LiveRecoverySequencer();

        Assert.Equal(LiveRecoveryAction.AdvanceCandidate, sequencer.Report(LivePlaybackFailureKind.Transport, hasNextCandidate: true));
        Assert.Equal(0, sequencer.PlayUrlRequests);
    }

    [Fact]
    public void Sequencer_ReRequestsPlayUrlWhenCandidatesAreExhausted()
    {
        var sequencer = new LiveRecoverySequencer();

        Assert.Equal(LiveRecoveryAction.ReRequestPlayUrl, sequencer.Report(LivePlaybackFailureKind.DecoderInit, hasNextCandidate: false));
        Assert.Equal(1, sequencer.PlayUrlRequests);
    }

    [Fact]
    public void Sequencer_ManualRetryWhenReRequestedCandidatesAlsoFail()
    {
        var sequencer = new LiveRecoverySequencer();
        Assert.Equal(LiveRecoveryAction.ReRequestPlayUrl, sequencer.Report(LivePlaybackFailureKind.Transport, false));
        sequencer.BeginAttempt();

        Assert.Equal(LiveRecoveryAction.ManualRetry, sequencer.Report(LivePlaybackFailureKind.Transport, false));
        Assert.Equal(1, sequencer.PlayUrlRequests);
    }

    [Fact]
    public void Sequencer_IgnoresEchoedFailureWithinTheSameAttempt()
    {
        var sequencer = new LiveRecoverySequencer();
        Assert.Equal(LiveRecoveryAction.AdvanceCandidate, sequencer.Report(LivePlaybackFailureKind.Transport, true));
        Assert.Equal(LiveRecoveryAction.None, sequencer.Report(LivePlaybackFailureKind.DecoderInit, true));
        Assert.Equal(LiveRecoveryAction.None, sequencer.Report(LivePlaybackFailureKind.Unknown, false));
    }

    [Fact]
    public void Sequencer_NewAttemptAcceptsTheNextFailure()
    {
        var sequencer = new LiveRecoverySequencer();
        sequencer.Report(LivePlaybackFailureKind.Transport, true);
        sequencer.BeginAttempt();

        Assert.Equal(LiveRecoveryAction.AdvanceCandidate, sequencer.Report(LivePlaybackFailureKind.DecoderInit, true));
    }

    [Fact]
    public void Sequencer_IgnoresCancellationAndResets()
    {
        var sequencer = new LiveRecoverySequencer();
        Assert.Equal(LiveRecoveryAction.None, sequencer.Report(LivePlaybackFailureKind.Cancelled, true));
        Assert.Equal(LiveRecoveryAction.None, sequencer.Report(LivePlaybackFailureKind.None, true));
        sequencer.Report(LivePlaybackFailureKind.Transport, false);
        sequencer.Reset();

        Assert.Equal(0, sequencer.PlayUrlRequests);
        Assert.Equal(LiveRecoveryAction.ReRequestPlayUrl, sequencer.Report(LivePlaybackFailureKind.Transport, false));
    }

    [Theory]
    [InlineData(LivePlaybackFailureKind.Transport, LiveFailureKind.AddressUnavailable)]
    [InlineData(LivePlaybackFailureKind.DecoderInit, LiveFailureKind.DecodeFailed)]
    [InlineData(LivePlaybackFailureKind.Cancelled, LiveFailureKind.Cancelled)]
    [InlineData(LivePlaybackFailureKind.Unknown, LiveFailureKind.Unknown)]
    public void ToLiveFailureKind_MapsForUserFacingMessage(LivePlaybackFailureKind source, LiveFailureKind expected)
    {
        Assert.Equal(expected, LivePlaybackFailureClassifier.ToLiveFailureKind(source));
    }
}
