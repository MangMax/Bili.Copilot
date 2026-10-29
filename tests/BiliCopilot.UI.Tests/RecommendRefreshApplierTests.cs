// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 推荐刷新结果（列表 + 新增条数）的组装规则。
/// </summary>
/// <remarks>
/// 流行页与分区页共用这一组装逻辑：增量时合并并给出新增条数，
/// 非增量时直接替换为新批次；提示文案由 <see cref="RefreshTipState"/> 统一派生。
/// </remarks>
public class RecommendRefreshApplierTests
{
    [Fact]
    public void Apply_IncrementalWithNewItems_ReportsAddedCount()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("x", "x"), new TestVideo("y", "y") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: true);

        Assert.Equal(new[] { "x", "y", "a" }, result.Items.Select(p => p.Id));
        Assert.Equal(2, result.AddedCount);
    }

    [Fact]
    public void Apply_IncrementalWithoutNewItems_ReportsZeroAdded()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("a", "a-new") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: true);

        Assert.Equal(new[] { "a" }, result.Items.Select(p => p.Id));
        Assert.Equal(0, result.AddedCount);
    }

    [Fact]
    public void Apply_Reloaded_ReturnsIncomingBatchWithZeroAdded()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("x", "x") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: false);

        Assert.Equal(new[] { "x" }, result.Items.Select(p => p.Id));
        Assert.Equal(0, result.AddedCount);
    }

    [Fact]
    public void Apply_NullIncoming_ProducesEmptyItemsWithZeroAdded()
    {
        var current = new[] { new TestVideo("a", "a") };

        var result = RecommendRefreshApplier.Apply<TestVideo>(
            current,
            null,
            p => p.Id,
            isIncremental: false);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.AddedCount);
    }

    private sealed record TestVideo(string Id, string Title);
}
