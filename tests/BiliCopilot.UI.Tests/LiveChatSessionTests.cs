using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 聊天会话核心：事件到列表的映射、屏蔽、醒目留言生命周期与计数。
/// </summary>
public sealed class LiveChatSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TrimBoundary_ReportsNewestMessageAndCountsIt()
    {
        var session = new LiveChatSession(retention: 3, slack: 1);
        for (var i = 0; i < 4; i++)
        {
            session.Apply(DanmakuEvent("m" + i, i, "u"), Now);
        }

        var change = session.Apply(DanmakuEvent("newest", 5, "u"), Now);
        Assert.Equal("newest", Assert.Single(change.Added).Text);
        Assert.Equal(2, change.Trimmed);
        Assert.Equal(5, session.Counters.Displayed);
        Assert.Equal(["m2", "m3", "newest"], session.Window.Items.Select(m => m.Text));
    }

    [Fact]
    public void DuplicateRows_RemainMergedAfterSuperChatDeletionAndTrimming()
    {
        var session = new LiveChatSession(retention: 3, slack: 1);
        session.Apply(SuperChatEvent("sc", "paid", 30, Now.AddMinutes(1)), Now);
        var original = Assert.Single(session.Apply(DanmakuEvent("same", 1, "u"), Now).Added);
        session.Apply(DanmakuEvent("same", 1, "u"), Now);
        session.Apply(SuperChatDeleteEvent("sc"), Now);
        Assert.Equal(2, Assert.Single(session.Window.Items).MergeCount);
        for (var i = 0; i < 3; i++)
        {
            session.Apply(DanmakuEvent("m" + i, i, "u"), Now);
        }

        Assert.Same(original, session.Window.Items[0]);
        session.Apply(DanmakuEvent("last", 9, "u"), Now);
        Assert.Equal(["m1", "m2", "last"], session.Window.Items.Select(m => m.Text));
    }

    [Fact]
    public void Apply_Danmaku_AddsMessageAndCounts()
    {
        var session = new LiveChatSession();

        var result = session.Apply(DanmakuEvent("hello", 1L, "u"), Now);

        var message = Assert.Single(result.Added);
        Assert.Equal(LiveChatMessageKind.Danmaku, message.Kind);
        Assert.Equal("hello", message.Text);
        Assert.False(result.RequiresReset);
        Assert.Equal(1, session.Counters.Received);
        Assert.Equal(1, session.Counters.Displayed);
    }

    [Fact]
    public void Apply_BlockedDanmakuNeverEntersList()
    {
        var session = new LiveChatSession(blockRules: new LiveChatBlockRules(keywords: ["spam"]));

        var result = session.Apply(DanmakuEvent("spam here", 1L, "u"), Now);

        Assert.Empty(result.Added);
        Assert.Empty(session.Window.Items);
        Assert.Equal(1, session.Counters.Blocked);
        Assert.Equal(0, session.Counters.Displayed);
        Assert.Equal(1, session.BlockCounters.ByKeyword);
    }

    [Fact]
    public void Apply_SuperChat_AddsCardWithPriceAndWindow()
    {
        var session = new LiveChatSession();

        var result = session.Apply(SuperChatEvent("sc-1", "paid", 30, Now.AddMinutes(1)), Now);

        var message = Assert.Single(result.Added);
        Assert.True(message.IsSuperChat);
        Assert.Equal(30, message.Price);
        Assert.Equal(Now.AddMinutes(1), message.EndTime);
        Assert.Equal(1, session.Counters.SuperChats);
        Assert.Equal("1:00", LiveChatSuperChatTime.FormatRemaining(
            LiveChatSuperChatTime.Remaining(message.EndTime, Now)));
    }

    [Fact]
    public void Apply_SuperChatDelete_RemovesCardAndRequiresReset()
    {
        var session = new LiveChatSession();
        _ = session.Apply(SuperChatEvent("sc-1", "paid", 30, Now.AddMinutes(1)), Now);
        _ = session.Apply(DanmakuEvent("after", 2L, "u"), Now);

        var result = session.Apply(SuperChatDeleteEvent("sc-1"), Now);

        Assert.True(result.RequiresReset);
        Assert.Equal(1, result.Removed);
        Assert.DoesNotContain(session.Window.Items, p => p.IsSuperChat);
        Assert.Single(session.Window.Items);
        Assert.Equal(1, session.Counters.SuperChatsRemoved);
    }

    [Fact]
    public void Apply_ConnectionState_AddsSystemMessageOncePerState()
    {
        var session = new LiveChatSession();

        var connecting = session.Apply(StateEvent(LiveDanmakuConnectionState.Connecting), Now);
        var connected = session.Apply(StateEvent(LiveDanmakuConnectionState.Connected), Now);
        var duplicate = session.Apply(StateEvent(LiveDanmakuConnectionState.Connected), Now);

        Assert.Equal(LiveChatSystemKind.Connecting, Assert.Single(connecting.Added).SystemKind);
        Assert.Equal(LiveChatSystemKind.Connected, Assert.Single(connected.Added).SystemKind);
        Assert.Empty(duplicate.Added);
        Assert.Equal(2, session.Counters.SystemMessages);
    }

    [Fact]
    public void Apply_RoomLifecycle_ProducesSystemMessages()
    {
        var session = new LiveChatSession();

        Assert.Equal(
            LiveChatSystemKind.RoomNotStarted,
            Assert.Single(session.Apply(new LiveRoomNotStartedEvent("PREPARING", Now), Now).Added).SystemKind);
        Assert.Equal(
            LiveChatSystemKind.RoomStarted,
            Assert.Single(session.Apply(new LiveStartedEvent("LIVE", Now), Now).Added).SystemKind);
        Assert.Equal(
            LiveChatSystemKind.RoomCutOff,
            Assert.Single(session.Apply(new LiveRoomCutOffEvent("bye", "CUT_OFF", Now), Now).Added).SystemKind);
    }

    [Fact]
    public void Apply_IgnoresNonChatEvents()
    {
        var session = new LiveChatSession();

        var result = session.Apply(new LiveOnlineCountChangedEvent(10, LiveOnlineCountSource.Popularity, "10", "ONLINE", Now), Now);

        Assert.Empty(result.Added);
        Assert.False(result.RequiresReset);
        Assert.Equal(0, session.Counters.Received);
    }

    [Fact]
    public void Apply_IsBoundedUnderFlood()
    {
        var session = new LiveChatSession(retention: 100, slack: 10);
        var resets = 0;

        for (var i = 0; i < 2_000; i++)
        {
            var result = session.Apply(DanmakuEvent("m" + i, i, "u"), Now);
            if (result.RequiresReset)
            {
                resets++;
            }

            Assert.InRange(session.Window.Count, 1, 110);
        }

        Assert.InRange(resets, 150, 200);
        Assert.Equal(2_000, session.Counters.Received);

        // 每条消息要么还在窗口里，要么被裁剪；不存在第三种去向。
        Assert.Equal(2_000, session.Counters.Trimmed + session.Window.Count);
    }

    [Fact]
    public void ExpireSuperChats_RemovesCardsPastEndTime()
    {
        var session = new LiveChatSession();
        _ = session.Apply(SuperChatEvent("sc-1", "short", 30, Now.AddSeconds(30)), Now);
        _ = session.Apply(SuperChatEvent("sc-2", "long", 100, Now.AddMinutes(5)), Now);

        var removed = session.ExpireSuperChats(Now.AddMinutes(1));

        Assert.Equal(1, removed);
        var remaining = Assert.Single(session.Window.Items);
        Assert.Equal("sc-2", remaining.SuperChatId);
    }

    [Fact]
    public void UpdateBlockRules_TakesEffectForExistingAndSubsequentMessages()
    {
        var session = new LiveChatSession();
        _ = session.Apply(DanmakuEvent("clean", 1L, "u"), Now);

        session.UpdateBlockRules(userIds: [1L], userNames: [], keywords: []);
        var blocked = session.Apply(DanmakuEvent("clean again", 1L, "u"), Now);

        Assert.Empty(blocked.Added);
        Assert.Equal(2, session.Counters.Blocked);
        Assert.Empty(session.Window.Items);
    }

    private static LiveDanmakuReceivedEvent DanmakuEvent(string text, long userId, string userName)
        => new(
            new LiveDanmakuInformation(text, "#FFFFFF", userName, "UL1", "#FFFFFF", false) { UserId = userId },
            "DANMU_MSG",
            Now);

    private static LiveSuperChatEvent SuperChatEvent(string id, string text, int price, DateTimeOffset end)
        => new(
            new LiveSuperChatInformation(id, text, price, "rich", 9L, Now, end, "#123456"),
            "SUPER_CHAT_MESSAGE",
            Now);

    private static LiveSuperChatDeletedEvent SuperChatDeleteEvent(params string[] ids)
        => new(ids, "SUPER_CHAT_MESSAGE_DELETE", Now);

    private static LiveConnectionStateChangedEvent StateEvent(LiveDanmakuConnectionState state)
        => new(state, null, Now);
}
