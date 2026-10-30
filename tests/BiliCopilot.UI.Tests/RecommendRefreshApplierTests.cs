// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 推荐刷新结果（合并或替换后的列表）的组装规则。
/// </summary>
/// <remarks>
/// 流行页与分区页共用这一组装逻辑：增量时合并到当前列表，
/// 非增量时直接替换为新批次；刷新完成后浮层只展示撤销入口，不再需要新增条数。
/// </remarks>
public class RecommendRefreshApplierTests
{
    [Fact]
    public void Apply_Incremental_PrependsNewItemsAndKeepsCurrent()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("x", "x"), new TestVideo("y", "y") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: true);

        Assert.Equal(new[] { "x", "y", "a" }, result.Items.Select(p => p.Id));
    }

    [Fact]
    public void Apply_IncrementalWithDuplicateKeys_KeepsSingleEntry()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("a", "a-new") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: true);

        Assert.Equal(new[] { "a" }, result.Items.Select(p => p.Id));
    }

    [Fact]
    public void Apply_Reloaded_ReturnsIncomingBatch()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("x", "x") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: false);

        Assert.Equal(new[] { "x" }, result.Items.Select(p => p.Id));
    }

    [Fact]
    public void Apply_NullIncoming_ProducesEmptyItems()
    {
        var current = new[] { new TestVideo("a", "a") };

        var result = RecommendRefreshApplier.Apply<TestVideo>(
            current,
            null,
            p => p.Id,
            isIncremental: false);

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Apply_NullKeySelector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RecommendRefreshApplier.Apply<TestVideo>(
            [],
            [],
            null!,
            isIncremental: true));
    }

    private sealed record TestVideo(string Id, string Title);
}
