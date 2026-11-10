using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 聊天条目：价格、剩余时间、舰长标记与系统文案的展示映射。
/// </summary>
public sealed class LiveChatMessagePresentationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SuperChatItem_ExposesPriceAndRemainingTime()
    {
        var item = Create(SuperChat("sc-1", 30, Now.AddSeconds(90)));

        Assert.True(item.IsSuperChat);
        Assert.Equal("¥30", item.PriceText);
        Assert.Equal("1:30", item.RemainingText);
        Assert.True(item.IsRemainingVisible);
    }

    [Fact]
    public void UpdateRemaining_AdvancesCountdownAndExpires()
    {
        var item = Create(SuperChat("sc-1", 30, Now.AddSeconds(30)));

        item.UpdateRemaining(Now.AddSeconds(10));
        Assert.Equal("0:20", item.RemainingText);
        Assert.True(item.IsRemainingVisible);

        item.UpdateRemaining(Now.AddSeconds(31));
        Assert.False(item.IsRemainingVisible);
        Assert.True(item.IsExpired);
    }

    [Fact]
    public void DanmakuItem_HasNoPriceOrRemaining()
    {
        var item = Create(Danmaku("hello", guardLevel: 0));

        Assert.False(item.IsSuperChat);
        Assert.Null(item.PriceText);
        Assert.Null(item.RemainingText);
        Assert.False(item.IsRemainingVisible);
    }

    [Theory]
    [InlineData(1, "总督")]
    [InlineData(2, "提督")]
    [InlineData(3, "舰长")]
    [InlineData(0, null)]
    public void DanmakuItem_MapsGuardBadge(int guardLevel, string? expected)
    {
        var item = Create(Danmaku("hello", guardLevel));

        Assert.Equal(expected, item.BadgeText);
    }

    [Fact]
    public void AdminAndVip_AreMarked()
    {
        var admin = Create(Danmaku("hello", 0, isAdmin: true));
        var vip = Create(Danmaku("hello", 0, isVip: true));

        Assert.Equal("管理", admin.BadgeText);
        Assert.Equal("老爷", vip.BadgeText);
    }

    [Fact]
    public void SystemItem_UsesProvidedTextResolver()
    {
        var item = new LiveChatMessagePresentation(
            LiveChatMessage.System(LiveChatSystemKind.RoomCutOff, 1, Now, "bye"),
            kind => "system:" + kind,
            Now);

        Assert.Equal("system:RoomCutOff", item.SystemText);
        Assert.Equal("bye", item.SystemDetail);
        Assert.True(item.IsSystem);
    }

    [Fact]
    public void SystemItem_FallsBackToKindNameWithoutResolver()
    {
        var item = new LiveChatMessagePresentation(LiveChatMessage.System(LiveChatSystemKind.Connected, 1, Now), null, Now);

        Assert.Equal("Connected", item.SystemText);
    }

    private static LiveChatMessagePresentation Create(LiveChatMessage message)
        => new(message, null, Now);

    private static LiveChatMessage Danmaku(string text, int guardLevel = 0, bool isAdmin = false, bool isVip = false)
        => LiveChatMessage.FromDanmaku(
            new LiveDanmakuInformation(text, "#FFFFFF", "user", "UL20", "#FFFFFF", isAdmin)
            {
                UserId = 42,
                GuardLevel = guardLevel,
                IsVip = isVip,
            },
            1,
            Now);

    private static LiveChatMessage SuperChat(string id, int price, DateTimeOffset end)
        => LiveChatMessage.FromSuperChat(
            new LiveSuperChatInformation(id, "paid message", price, "rich", 9L, Now, end, "#123456"),
            1);
}
