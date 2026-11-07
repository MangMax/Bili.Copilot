using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播间状态与在线人数的纯逻辑：人数单位、开播时长、下播/切断终态、开播恢复、
/// 重载去重与换房后旧事件不生效。
/// </summary>
public sealed class LiveRoomStatusTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(9999, "9999")]
    [InlineData(10000, "1万")]
    [InlineData(12345, "1.23万")]
    [InlineData(2330000, "233万")]
    [InlineData(100000000, "1亿")]
    [InlineData(123456789, "1.23亿")]
    public void FormatCount_UsesChineseUnits(long count, string expected)
        => Assert.Equal(expected, LiveRoomStatusText.FormatCount(count, "万", "亿"));

    [Fact]
    public void FormatCount_NegativeIsEmpty()
        => Assert.Equal(string.Empty, LiveRoomStatusText.FormatCount(-1, "万", "亿"));

    [Theory]
    [InlineData(0, "0分钟")]
    [InlineData(59, "0分钟")]
    [InlineData(60, "1分钟")]
    [InlineData(3599, "59分钟")]
    [InlineData(3600, "1小时")]
    [InlineData(3660, "1小时1分钟")]
    [InlineData(7380, "2小时3分钟")]
    public void FormatDuration_UsesMinutesAndHours(int seconds, string expected)
        => Assert.Equal(
            expected,
            LiveRoomStatusText.FormatDuration(TimeSpan.FromSeconds(seconds), "{0}分钟", "{0}小时", "{0}小时{1}分钟"));

    [Fact]
    public void FormatDuration_NegativeClampsToZero()
        => Assert.Equal(
            "0分钟",
            LiveRoomStatusText.FormatDuration(TimeSpan.FromSeconds(-30), "{0}分钟", "{0}小时", "{0}小时{1}分钟"));

    [Fact]
    public void NotStartedAndCutOff_AreTerminalStates()
    {
        var tracker = new LiveRoomStatusTracker();
        tracker.SetRoom("123");
        tracker.ApplyRoomDetail(isLiving: true);
        Assert.Equal(LiveRoomPhase.Living, tracker.Phase);

        Assert.True(tracker.ApplyNotStarted("123"));
        Assert.Equal(LiveRoomPhase.NotStarted, tracker.Phase);
        Assert.True(tracker.IsTerminal);

        Assert.True(tracker.ApplyCutOff("123"));
        Assert.Equal(LiveRoomPhase.CutOff, tracker.Phase);
        Assert.True(tracker.IsTerminal);
    }

    [Fact]
    public void StartedEvent_RecoversTerminalRoom()
    {
        var tracker = new LiveRoomStatusTracker();
        tracker.SetRoom("123");
        tracker.ApplyNotStarted("123");

        Assert.True(tracker.ApplyStarted("123"));
        Assert.Equal(LiveRoomPhase.Living, tracker.Phase);
        Assert.False(tracker.IsTerminal);
    }

    [Fact]
    public void StartedEvent_RepeatedDoesNotReportChange()
    {
        var tracker = new LiveRoomStatusTracker();
        tracker.SetRoom("123");
        tracker.ApplyRoomDetail(isLiving: true);

        Assert.False(tracker.ApplyStarted("123"));
    }

    [Fact]
    public void EventsFromAnotherRoom_AreIgnored()
    {
        var tracker = new LiveRoomStatusTracker();
        tracker.SetRoom("123");
        tracker.ApplyRoomDetail(isLiving: true);

        Assert.False(tracker.ApplyNotStarted("456"));
        Assert.False(tracker.ApplyCutOff("456"));
        Assert.False(tracker.ApplyStarted("456"));
        Assert.False(tracker.ApplyOnlineCount("456", 10, LiveOnlineCountSource.Watched));
        Assert.False(tracker.TryBeginReload("456", DateTimeOffset.UnixEpoch));
        Assert.Equal(LiveRoomPhase.Living, tracker.Phase);
        Assert.Null(tracker.OnlineCount);
    }

    [Fact]
    public void WatchedCount_WinsOverPopularity()
    {
        var tracker = new LiveRoomStatusTracker();
        tracker.SetRoom("123");

        Assert.True(tracker.ApplyOnlineCount("123", 5000, LiveOnlineCountSource.Watched));
        Assert.Equal(5000, tracker.OnlineCount);

        Assert.False(tracker.ApplyOnlineCount("123", 90000, LiveOnlineCountSource.Popularity));
        Assert.Equal(5000, tracker.OnlineCount);
        Assert.Equal(LiveOnlineCountSource.Watched, tracker.OnlineCountSource);
    }

    [Fact]
    public void PopularityCount_IsReplacedByWatched()
    {
        var tracker = new LiveRoomStatusTracker();
        tracker.SetRoom("123");

        Assert.True(tracker.ApplyOnlineCount("123", 90000, LiveOnlineCountSource.Popularity));
        Assert.True(tracker.ApplyOnlineCount("123", 321, LiveOnlineCountSource.Watched));
        Assert.Equal(321, tracker.OnlineCount);
        Assert.Equal(LiveOnlineCountSource.Watched, tracker.OnlineCountSource);
    }

    [Fact]
    public void Reload_IsDedupedWithinCooldown()
    {
        var tracker = new LiveRoomStatusTracker(TimeSpan.FromSeconds(5));
        tracker.SetRoom("123");
        var start = DateTimeOffset.UnixEpoch;

        Assert.True(tracker.TryBeginReload("123", start));
        Assert.False(tracker.TryBeginReload("123", start.AddSeconds(1)));
        Assert.False(tracker.TryBeginReload("123", start.AddSeconds(4.9)));
        Assert.True(tracker.TryBeginReload("123", start.AddSeconds(5)));
        Assert.Equal(2, tracker.ReloadCount);
    }

    [Fact]
    public void Reload_IsRejectedInTerminalState()
    {
        var tracker = new LiveRoomStatusTracker();
        tracker.SetRoom("123");
        tracker.ApplyNotStarted("123");

        Assert.False(tracker.TryBeginReload("123", DateTimeOffset.UnixEpoch));
        Assert.Equal(0, tracker.ReloadCount);
    }

    [Fact]
    public void Reload_CooldownRestartsForNewRoom()
    {
        var tracker = new LiveRoomStatusTracker(TimeSpan.FromSeconds(30));
        tracker.SetRoom("123");
        Assert.True(tracker.TryBeginReload("123", DateTimeOffset.UnixEpoch));

        tracker.SetRoom("456");
        Assert.True(tracker.TryBeginReload("456", DateTimeOffset.UnixEpoch.AddSeconds(1)));
        Assert.Equal(1, tracker.ReloadCount);
        Assert.Null(tracker.OnlineCount);
        Assert.Equal(LiveRoomPhase.Unknown, tracker.Phase);
    }

    [Fact]
    public void RoomDetail_DecidesInitialPhase()
    {
        var tracker = new LiveRoomStatusTracker();
        tracker.SetRoom("123");

        tracker.ApplyRoomDetail(isLiving: false);
        Assert.Equal(LiveRoomPhase.NotStarted, tracker.Phase);

        tracker.ApplyRoomDetail(isLiving: true);
        Assert.Equal(LiveRoomPhase.Living, tracker.Phase);
    }
}
