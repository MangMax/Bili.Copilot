using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 醒目留言剩余时间与系统消息映射（纯逻辑，不依赖 WinUI）。
/// </summary>
public sealed class LiveChatPresentationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(65, "1:05")]
    [InlineData(600, "10:00")]
    [InlineData(3661, "61:01")]
    public void Remaining_FormatsMinutesAndSeconds(int seconds, string expected)
    {
        var remaining = LiveChatSuperChatTime.Remaining(Now.AddSeconds(seconds), Now);

        Assert.Equal(expected, LiveChatSuperChatTime.FormatRemaining(remaining));
    }

    [Fact]
    public void Remaining_IsClampedToZeroAfterEnd()
    {
        var remaining = LiveChatSuperChatTime.Remaining(Now.AddSeconds(-30), Now);

        Assert.Equal(TimeSpan.Zero, remaining);
        Assert.True(LiveChatSuperChatTime.IsExpired(Now.AddSeconds(-1), Now));
        Assert.False(LiveChatSuperChatTime.IsExpired(Now.AddSeconds(1), Now));
    }

    [Theory]
    [InlineData(30, "¥30")]
    [InlineData(100, "¥100")]
    [InlineData(0, "¥0")]
    public void Price_IsFormattedWithCurrencyPrefix(int price, string expected)
        => Assert.Equal(expected, LiveChatSuperChatTime.FormatPrice(price));

    [Theory]
    [InlineData(LiveDanmakuConnectionState.Connecting, LiveChatSystemKind.Connecting)]
    [InlineData(LiveDanmakuConnectionState.Connected, LiveChatSystemKind.Connected)]
    [InlineData(LiveDanmakuConnectionState.Reconnecting, LiveChatSystemKind.Reconnecting)]
    [InlineData(LiveDanmakuConnectionState.Closed, LiveChatSystemKind.Disconnected)]
    public void FromConnectionState_MapsEveryState(LiveDanmakuConnectionState state, LiveChatSystemKind expected)
    {
        var message = LiveChatSystemMessage.FromConnectionState(state, "detail", 5, Now);

        Assert.Equal(LiveChatMessageKind.System, message.Kind);
        Assert.Equal(expected, message.SystemKind);
        Assert.Equal("detail", message.SystemDetail);
        Assert.Equal(5, message.Sequence);
        Assert.Equal(Now, message.Timestamp);
    }

    [Fact]
    public void FromConnectionState_BlankDetailBecomesNull()
    {
        var message = LiveChatSystemMessage.FromConnectionState(LiveDanmakuConnectionState.Closed, "  ", 1, Now);

        Assert.Null(message.SystemDetail);
    }

    [Fact]
    public void FromEvent_MapsRoomLifecycleEvents()
    {
        Assert.Equal(
            LiveChatSystemKind.RoomNotStarted,
            LiveChatSystemMessage.FromEvent(new LiveRoomNotStartedEvent("PREPARING", Now), 1, Now)!.SystemKind);
        Assert.Equal(
            LiveChatSystemKind.RoomStarted,
            LiveChatSystemMessage.FromEvent(new LiveStartedEvent("LIVE", Now), 1, Now)!.SystemKind);

        var cutOff = LiveChatSystemMessage.FromEvent(new LiveRoomCutOffEvent("byebye", "CUT_OFF", Now), 1, Now);
        Assert.NotNull(cutOff);
        Assert.Equal(LiveChatSystemKind.RoomCutOff, cutOff!.SystemKind);
        Assert.Equal("byebye", cutOff.SystemDetail);
    }

    [Fact]
    public void FromEvent_ReturnsNullForNonSystemEvents()
    {
        var danmaku = new LiveDanmakuReceivedEvent(
            new LiveDanmakuInformation("hi", "#FFF", "u", "UL1", "#FFF", false),
            "DANMU_MSG",
            Now);

        Assert.Null(LiveChatSystemMessage.FromEvent(danmaku, 1, Now));
        Assert.Null(LiveChatSystemMessage.FromEvent(
            new LiveConnectionStateChangedEvent(LiveDanmakuConnectionState.Connected, null, Now), 1, Now));
        Assert.Null(LiveChatSystemMessage.FromEvent(null, 1, Now));
    }

    [Fact]
    public void AuthenticationFailure_ProducesDedicatedSystemKind()
    {
        var message = LiveChatSystemMessage.FromAuthenticationFailure("code=-101", 2, Now);

        Assert.Equal(LiveChatSystemKind.AuthenticationFailed, message.SystemKind);
        Assert.Equal("code=-101", message.SystemDetail);
    }
}
