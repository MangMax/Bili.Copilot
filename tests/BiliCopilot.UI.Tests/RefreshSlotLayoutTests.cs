// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 增量刷新占位区排布测试.
/// </summary>
public class RefreshSlotLayoutTests
{
    [Fact]
    public void BeforeAnyPageArrivesEverySlotIsAPlaceholder()
    {
        var layout = RefreshSlotLayout.Compute(totalCount: 20, baselineCount: 20, pendingCount: 30, budget: 30);

        Assert.Equal(0, layout.Inserted);
        Assert.Equal(30, layout.Remaining);
        Assert.Equal(20, layout.OldCount);
        Assert.Equal(50, layout.TotalCount);
        Assert.All(Enumerable.Range(0, 30), i => Assert.Equal(-1, layout.VideoIndexAt(i)));
    }

    [Fact]
    public void NewCardsFillTheReservedAreaFromTheTop()
    {
        // 旧列表 20 条，刷新目标 30 条，第一页取回 6 条。
        var layout = RefreshSlotLayout.Compute(totalCount: 26, baselineCount: 20, pendingCount: 30, budget: 30);

        Assert.Equal(6, layout.Inserted);
        Assert.Equal(24, layout.Remaining);
        // 前 6 个槽位是新卡片，按到达顺序占位。
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, Enumerable.Range(0, 6).Select(layout.VideoIndexAt));
        // 之后仍是占位，再往后是旧卡片。
        Assert.Equal(-1, layout.VideoIndexAt(6));
        Assert.Equal(6, layout.VideoIndexAt(30));
        Assert.Equal(25, layout.VideoIndexAt(49));
    }

    [Fact]
    public void ReservedAreaKeepsItsHeightSoOldContentNeverMoves()
    {
        var first = RefreshSlotLayout.Compute(totalCount: 26, baselineCount: 20, pendingCount: 30, budget: 30);
        var second = RefreshSlotLayout.Compute(totalCount: 32, baselineCount: 20, pendingCount: 30, budget: 30);

        // 预留区（新卡片 + 剩余占位）高度恒定，旧内容所在的槽位因此不动。
        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.Equal(30, first.Inserted + first.Remaining);
        Assert.Equal(30, second.Inserted + second.Remaining);
        // 槽位 30 在两次同步里都是「旧列表第一条」，只是它在业务列表里的下标随新卡片增加而后移。
        Assert.Equal(first.Inserted, first.VideoIndexAt(30));
        Assert.Equal(second.Inserted, second.VideoIndexAt(30));
    }

    [Fact]
    public void FillCompletesWithoutChangingTheVisibleSequence()
    {
        var whileRefreshing = RefreshSlotLayout.Compute(totalCount: 50, baselineCount: 20, pendingCount: 30, budget: 30);
        var completed = RefreshSlotLayout.Compute(totalCount: 50, baselineCount: 20, pendingCount: 0, budget: 30);

        Assert.Equal(0, completed.Remaining);
        Assert.Equal(whileRefreshing.TotalCount, completed.TotalCount);
        for (var i = 0; i < completed.TotalCount; i++)
        {
            Assert.Equal(whileRefreshing.VideoIndexAt(i), completed.VideoIndexAt(i));
        }
    }

    [Fact]
    public void ShortRefreshCollapsesTheUnusedPlaceholdersWhenItEnds()
    {
        // 只取回 4 条就结束：刷新收尾（pending 归零）后剩余占位一次性收回，
        // 由尾巴占位补齐到视口预算。
        var layout = RefreshSlotLayout.Compute(totalCount: 24, baselineCount: 20, pendingCount: 0, budget: 30);

        Assert.Equal(0, layout.Inserted);
        Assert.Equal(0, layout.Remaining);
        Assert.Equal(24, layout.OldCount);
        Assert.Equal(6, layout.TailCount);
        Assert.Equal(-1, layout.VideoIndexAt(24));
    }

    [Fact]
    public void TailPlaceholdersOnlyFillUpToTheViewportBudget()
    {
        var shortList = RefreshSlotLayout.Compute(totalCount: 6, baselineCount: 0, pendingCount: 0, budget: 30);
        Assert.Equal(24, shortList.TailCount);

        var longList = RefreshSlotLayout.Compute(totalCount: 60, baselineCount: 0, pendingCount: 0, budget: 30);
        Assert.Equal(0, longList.TailCount);
    }
}