using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 聊天列表窗口：保留约 500 条，裁剪按批进行，只有批量裁剪才要求整表重置。
/// </summary>
public sealed class LiveChatWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Append_AddsIncrementallyWithoutReset()
    {
        var window = new LiveChatWindow();

        var change = window.Append(Danmaku("a"));

        Assert.False(change.RequiresReset);
        Assert.NotNull(change.Added);
        Assert.Equal(0, change.Trimmed);
        Assert.Single(window.Items);
    }

    [Fact]
    public void Append_TrimsInChunksAndOnlyThenRequiresReset()
    {
        var window = new LiveChatWindow(retention: 10, slack: 5);

        for (var i = 0; i < 15; i++)
        {
            Assert.False(window.Append(Danmaku("m" + i)).RequiresReset);
        }

        // 第 16 条把窗口推到 16 条，超过 10+5，一次性裁到 10 条。
        var change = window.Append(Danmaku("m15"));

        Assert.True(change.RequiresReset);
        Assert.Equal(6, change.Trimmed);
        Assert.Equal(10, window.Items.Count);
        Assert.Equal("m6", window.Items[0].Text);
        Assert.Equal("m15", window.Items[^1].Text);
    }

    [Fact]
    public void Window_NeverExceedsRetentionPlusSlack()
    {
        var window = new LiveChatWindow(retention: 500, slack: 50);

        var resets = 0;
        for (var i = 0; i < 5_000; i++)
        {
            if (window.Append(Danmaku("m" + i)).RequiresReset)
            {
                resets++;
                Assert.Equal(500, window.Items.Count);
            }

            Assert.InRange(window.Items.Count, 1, 550);
        }

        // 5000 条 / 每 50 条一次重置 ≈ 90 次，远少于逐条裁剪的 4500 次。
        Assert.InRange(resets, 80, 100);
    }

    [Fact]
    public void RemoveSuperChat_ReportsResetWithRemovedCount()
    {
        var window = new LiveChatWindow(retention: 10, slack: 5);
        window.Append(Danmaku("a"));
        window.Append(SuperChat("sc-1"));
        window.Append(Danmaku("b"));

        var change = window.RemoveSuperChat(["sc-1"]);

        Assert.True(change.RequiresReset);
        Assert.Equal(1, change.Removed);
        Assert.Equal(["a", "b"], window.Items.Select(p => p.Text));
    }

    [Fact]
    public void RemoveSuperChat_UnknownIdIsNoOp()
    {
        var window = new LiveChatWindow();
        window.Append(Danmaku("a"));

        var change = window.RemoveSuperChat(["missing"]);

        Assert.False(change.RequiresReset);
        Assert.Equal(0, change.Removed);
        Assert.Single(window.Items);
    }

    [Fact]
    public void Clear_EmptiesWindow()
    {
        var window = new LiveChatWindow();
        window.Append(Danmaku("a"));

        window.Clear();

        Assert.Empty(window.Items);
        Assert.Equal(0, window.Count);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LiveChatWindow(retention: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LiveChatWindow(retention: 10, slack: -1));
    }

    private static LiveChatMessage Danmaku(string text)
        => LiveChatMessage.FromDanmaku(
            new LiveDanmakuInformation(text, "#FFF", "u", "UL1", "#FFF", false) { UserId = 1L },
            0,
            Now);

    private static LiveChatMessage SuperChat(string id)
        => LiveChatMessage.FromSuperChat(
            new LiveSuperChatInformation(id, "paid", 30, "u", 1L, Now, Now.AddMinutes(1), null),
            0);
}
