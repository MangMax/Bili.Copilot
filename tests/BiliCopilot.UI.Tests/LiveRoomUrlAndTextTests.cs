// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播间网页地址的唯一构造点（#192 去重）与聊天徽标/金额的可注入文案。
/// </summary>
public sealed class LiveRoomUrlAndTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("12345", "https://live.bilibili.com/12345")]
    [InlineData("  12345  ", "https://live.bilibili.com/12345")]
    public void Create_BuildsTheSameRoomUrlEverywhere(string roomId, string expected)
        => Assert.Equal(expected, LiveRoomUrl.CreateString(roomId));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsNullForMissingRoomId(string? roomId)
    {
        Assert.Null(LiveRoomUrl.Create(roomId));
        Assert.Equal(string.Empty, LiveRoomUrl.CreateString(roomId));
    }

    [Fact]
    public void BadgeText_CanBeOverriddenByLocalizedResolver()
    {
        var message = Danmaku("hello", guardLevel: 1);
        var item = new LiveChatMessagePresentation(message, null, Now, kind => "badge:" + kind, price => "price:" + price);

        Assert.Equal("badge:Governor", item.BadgeText);
    }

    [Theory]
    [InlineData(1, LiveChatBadgeKind.Governor)]
    [InlineData(2, LiveChatBadgeKind.Admiral)]
    [InlineData(3, LiveChatBadgeKind.Captain)]
    public void GuardLevel_MapsToTheMatchingBadgeKind(int guardLevel, LiveChatBadgeKind expected)
    {
        LiveChatBadgeKind? captured = null;
        var item = new LiveChatMessagePresentation(
            Danmaku("hello", guardLevel),
            null,
            Now,
            kind =>
            {
                captured = kind;
                return "x";
            });

        Assert.Equal(expected, captured);
        Assert.Equal("x", item.BadgeText);
    }

    [Fact]
    public void BadgeText_FallsBackToBuiltInChineseWithoutResolver()
    {
        Assert.Equal("总督", new LiveChatMessagePresentation(Danmaku("a", 1), null, Now).BadgeText);
        Assert.Equal("提督", new LiveChatMessagePresentation(Danmaku("a", 2), null, Now).BadgeText);
        Assert.Equal("舰长", new LiveChatMessagePresentation(Danmaku("a", 3), null, Now).BadgeText);
        Assert.Equal("管理", new LiveChatMessagePresentation(Danmaku("a", 0, isAdmin: true), null, Now).BadgeText);
        Assert.Equal("老爷", new LiveChatMessagePresentation(Danmaku("a", 0, isVip: true), null, Now).BadgeText);
    }

    [Fact]
    public void PriceText_CanBeOverriddenByLocalizedTemplate()
    {
        var message = LiveChatMessage.FromSuperChat(
            new Richasy.BiliKernel.Models.Media.LiveSuperChatInformation(
                "sc-1",
                "paid",
                30,
                "rich",
                9L,
                Now,
                Now.AddMinutes(1),
                "#123456"),
            1);

        var item = new LiveChatMessagePresentation(message, null, Now, null, price => LiveChatSuperChatTime.FormatPrice(price, "￥{0}"));

        Assert.Equal("￥30", item.PriceText);
    }

    [Fact]
    public void DefaultPriceTemplate_KeepsTheDocumentedYenPrefix()
        => Assert.Equal("¥30", LiveChatSuperChatTime.FormatPrice(30, LiveChatSuperChatTime.DefaultPriceTemplate));

    [Fact]
    public void LateRoomEmotesUpdateExistingPresentationAndNotifyBindings()
    {
        var item = new LiveChatMessagePresentation(Danmaku("[doge]"), null, Now);
        var changed = new List<string?>();
        item.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        Assert.Null(item.Content.Emotes);

        item.UpdateEmotes(new Dictionary<string, Richasy.BiliKernel.Models.Media.LiveDanmakuEmote>
        {
            ["doge"] = new("https://example.com/doge.png", 24, 24, "official_1"),
        });

        Assert.Equal("https://example.com/doge.png", item.Content.Emotes!["[doge]"].Uri.AbsoluteUri);
        Assert.Equal([nameof(item.Content)], changed);
        Assert.Equal("[doge]", item.Text);
    }

    private static LiveChatMessage Danmaku(string text, int guardLevel = 0, bool isAdmin = false, bool isVip = false)
        => LiveChatMessage.FromDanmaku(
            new Richasy.BiliKernel.Models.Media.LiveDanmakuInformation(text, "#FFFFFF", "user", "UL20", "#FFFFFF", isAdmin)
            {
                UserId = 42,
                GuardLevel = guardLevel,
                IsVip = isVip,
            },
            1,
            Now);
}
