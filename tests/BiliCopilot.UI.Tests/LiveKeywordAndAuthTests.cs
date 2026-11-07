// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 关键词高亮（#196）与认证失败系统消息（#197）的纯逻辑映射。
/// </summary>
public sealed class LiveKeywordAndAuthTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyKeywordSetting_MatchesNothing()
    {
        var matcher = LiveDanmakuKeywordMatcher.Parse("  ");

        Assert.Equal(0, matcher.Count);
        Assert.False(matcher.IsMatch("任意弹幕"));
    }

    [Fact]
    public void NullKeywordSetting_ReturnsSharedEmptyMatcher()
        => Assert.Same(LiveDanmakuKeywordMatcher.Empty, LiveDanmakuKeywordMatcher.Parse(null));

    [Theory]
    [InlineData("抽奖,福利")]
    [InlineData("抽奖；福利")]
    [InlineData("抽奖\n福利")]
    [InlineData("抽奖 , 福利 ")]
    public void Keywords_SplitOnCommaSemicolonAndNewline(string text)
    {
        var matcher = LiveDanmakuKeywordMatcher.Parse(text);

        Assert.Equal(2, matcher.Count);
        Assert.True(matcher.IsMatch("今晚抽奖"));
        Assert.True(matcher.IsMatch("福利来了"));
        Assert.False(matcher.IsMatch("普通弹幕"));
    }

    [Fact]
    public void KeywordMatching_IsCaseInsensitiveAndDeduplicated()
    {
        var matcher = LiveDanmakuKeywordMatcher.Parse("Giveaway,giveaway,GIVEAWAY");

        Assert.Equal(1, matcher.Count);
        Assert.True(matcher.IsMatch("join the GIVEAWAY now"));
    }

    [Fact]
    public void KeywordHit_RaisesCandidateToHighlightedTier()
    {
        var matcher = LiveDanmakuKeywordMatcher.Parse("抽奖");
        var candidate = new LiveDanmakuCandidate
        {
            Text = "今晚抽奖",
            IsKeywordHighlighted = matcher.IsMatch("今晚抽奖"),
        };

        Assert.Equal(LiveDanmakuPriority.Highlighted, candidate.Priority);
        Assert.True(candidate.Priority > LiveDanmakuPriority.Normal);
    }

    [Fact]
    public void UnauthorizedException_IsClassifiedAsAuthenticationFailure()
    {
        var exception = new LiveDanmakuUnauthorizedException(-101, "未登录");

        Assert.True(LiveDanmakuAuthFailure.IsAuthenticationFailure(exception));
        Assert.Contains("认证失败", LiveDanmakuAuthFailure.GetDetail(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("网络错误")]
    [InlineData("")]
    public void NonAuthException_IsNotClassifiedAsAuthenticationFailure(string? message)
    {
        Exception? exception = message is null ? new IOException("boom") : new InvalidOperationException(message);

        Assert.False(LiveDanmakuAuthFailure.IsAuthenticationFailure(exception));
        Assert.Null(LiveDanmakuAuthFailure.GetDetail(exception));
    }

    [Theory]
    [InlineData("直播弹幕认证失败：code=-101", true)]
    [InlineData("认证过期", true)]
    [InlineData("连接被重置", false)]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    public void AuthenticationDetail_IsDetectedFromServerText(string? detail, bool expected)
        => Assert.Equal(expected, LiveDanmakuAuthFailure.IsAuthenticationDetail(detail));

    [Fact]
    public void ApplyAuthenticationFailure_AddsSystemMessageWithSuppliedTimestamp()
    {
        // 假时钟：系统消息必须使用调用方传入的时间，而不是真实时钟。
        var session = new LiveChatSession();
        var fakeNow = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var result = session.ApplyAuthenticationFailure("code=-101", fakeNow);

        var message = Assert.Single(result.Added);
        Assert.Equal(LiveChatMessageKind.System, message.Kind);
        Assert.Equal(LiveChatSystemKind.AuthenticationFailed, message.SystemKind);
        Assert.Equal(fakeNow, message.Timestamp);
        Assert.Equal("code=-101", message.SystemDetail);
        Assert.Equal(1, session.Counters.SystemMessages);
    }

    [Fact]
    public void ApplyConnectionState_UsesSuppliedTimestampForSystemMessage()
    {
        var session = new LiveChatSession();
        var fakeNow = new DateTimeOffset(2030, 5, 6, 7, 8, 9, TimeSpan.Zero);

        var result = session.ApplyConnectionState(LiveDanmakuConnectionState.Connected, null, fakeNow);

        Assert.Equal(fakeNow, Assert.Single(result.Added).Timestamp);
    }

    [Fact]
    public void Apply_ConnectionEventUsesSuppliedNowInsteadOfEventTimestamp()
    {
        var session = new LiveChatSession();
        var fakeNow = new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var staleEventTime = fakeNow.AddHours(-3);

        var result = session.Apply(
            new LiveConnectionStateChangedEvent(LiveDanmakuConnectionState.Reconnecting, "重连", staleEventTime),
            fakeNow);

        Assert.Equal(fakeNow, Assert.Single(result.Added).Timestamp);
    }

    [Fact]
    public void ApplyRoomEvent_UsesSuppliedNowInsteadOfEventTimestamp()
    {
        var session = new LiveChatSession();
        var fakeNow = new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);

        var result = session.Apply(new LiveRoomCutOffEvent("被切断", "cut-off", fakeNow.AddHours(-1)), fakeNow);

        var message = Assert.Single(result.Added);
        Assert.Equal(LiveChatSystemKind.RoomCutOff, message.SystemKind);
        Assert.Equal(fakeNow, message.Timestamp);
    }
}
