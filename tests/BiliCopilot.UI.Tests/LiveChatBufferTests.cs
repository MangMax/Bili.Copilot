using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播聊天有界缓冲：容量上限、顺序、裁剪与醒目留言移除。
/// </summary>
public sealed class LiveChatBufferTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Append_KeepsMessagesInArrivalOrder()
    {
        var buffer = new LiveChatBuffer(4);

        buffer.Append(Danmaku("a", 1L, "u1"));
        buffer.Append(Danmaku("b", 2L, "u2"));

        Assert.Equal(["a", "b"], buffer.Items.Select(p => p.Text));
        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public void Append_DropsOldestWhenCapacityExceeded_AndReportsTrim()
    {
        var buffer = new LiveChatBuffer(3);

        Assert.Equal(0, buffer.Append(Danmaku("a")));
        Assert.Equal(0, buffer.Append(Danmaku("b")));
        Assert.Equal(0, buffer.Append(Danmaku("c")));
        Assert.Equal(1, buffer.Append(Danmaku("d")));
        Assert.Equal(1, buffer.Append(Danmaku("e")));

        Assert.Equal(["c", "d", "e"], buffer.Items.Select(p => p.Text));
        Assert.Equal(5, buffer.TotalAppended);
        Assert.Equal(2, buffer.TotalTrimmed);
    }

    [Fact]
    public void Append_StaysBoundedAcrossFlood()
    {
        var buffer = new LiveChatBuffer(500);

        for (var i = 0; i < 20_000; i++)
        {
            _ = buffer.Append(Danmaku("m" + i));
        }

        Assert.Equal(500, buffer.Count);
        Assert.Equal(20_000, buffer.TotalAppended);
        Assert.Equal(19_500, buffer.TotalTrimmed);
        Assert.Equal("m19999", buffer.Items[^1].Text);
        Assert.Equal("m19500", buffer.Items[0].Text);
    }

    [Fact]
    public void RemoveSuperChat_DropsMatchingCard_AndKeepsOrder()
    {
        var buffer = new LiveChatBuffer(10);
        buffer.Append(Danmaku("a"));
        buffer.Append(SuperChat("sc-1", "paid", 30));
        buffer.Append(Danmaku("b"));
        buffer.Append(SuperChat("sc-2", "other", 50));

        var removed = buffer.RemoveSuperChat(["sc-1"]);

        Assert.Equal(1, removed);
        Assert.Equal(["a", "b", "other"], buffer.Items.Select(p => p.Text));
        Assert.Single(buffer.Items, p => p.Kind == LiveChatMessageKind.SuperChat);
    }

    [Fact]
    public void RemoveSuperChat_IgnoresUnknownIds()
    {
        var buffer = new LiveChatBuffer(4);
        buffer.Append(SuperChat("sc-1", "paid", 30));

        Assert.Equal(0, buffer.RemoveSuperChat(["nope"]));
        Assert.Equal(0, buffer.RemoveSuperChat([]));
        Assert.Equal(0, buffer.RemoveSuperChat(null));
        Assert.Single(buffer.Items);
    }

    [Fact]
    public void Clear_ResetsMessagesButKeepsLifetimeCounters()
    {
        var buffer = new LiveChatBuffer(2);
        buffer.Append(Danmaku("a"));
        buffer.Append(Danmaku("b"));
        buffer.Append(Danmaku("c"));

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Items);
        Assert.Equal(3, buffer.TotalAppended);
        Assert.Equal(1, buffer.TotalTrimmed);
    }

    [Fact]
    public void Capacity_MustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LiveChatBuffer(0));
        Assert.Equal(LiveChatBuffer.DefaultCapacity, new LiveChatBuffer().Capacity);
        Assert.Equal(500, LiveChatBuffer.DefaultCapacity);
    }

    [Fact]
    public void FromDanmaku_CarriesUserAndGuardInformation()
    {
        var danmaku = new LiveDanmakuInformation("hello", "#FF0000", "user", "UL20", "#00FF00", true)
        {
            UserId = 42,
            GuardLevel = 3,
        };

        var message = LiveChatMessage.FromDanmaku(danmaku, 7, Now);

        Assert.Equal(LiveChatMessageKind.Danmaku, message.Kind);
        Assert.Equal(7, message.Sequence);
        Assert.Equal("hello", message.Text);
        Assert.Equal(42, message.UserId);
        Assert.Equal("user", message.UserName);
        Assert.Equal(3, message.GuardLevel);
        Assert.True(message.IsAdmin);
        Assert.Equal(Now, message.Timestamp);
    }

    [Fact]
    public void FromSuperChat_CarriesPriceAndWindow()
    {
        var info = new LiveSuperChatInformation("sc-1", "hi", 100, "rich", 9L, Now, Now.AddMinutes(2), "#123456");

        var message = LiveChatMessage.FromSuperChat(info, 3);

        Assert.Equal(LiveChatMessageKind.SuperChat, message.Kind);
        Assert.Equal("sc-1", message.SuperChatId);
        Assert.Equal(100, message.Price);
        Assert.Equal(Now.AddMinutes(2), message.EndTime);
        Assert.Equal("#123456", message.BackgroundColor);
    }

    private static LiveChatMessage Danmaku(string text, long userId = 1L, string userName = "u")
        => LiveChatMessage.FromDanmaku(
            new LiveDanmakuInformation(text, "#FFFFFF", userName, "UL1", "#FFFFFF", false) { UserId = userId },
            0,
            Now);

    private static LiveChatMessage SuperChat(string id, string text, int price)
        => LiveChatMessage.FromSuperChat(
            new LiveSuperChatInformation(id, text, price, "u", 1L, Now, Now.AddMinutes(1), null),
            0);
}
