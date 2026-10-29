// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 推荐刷新结果（列表 + 提示文案）的组装规则。
/// </summary>
/// <remarks>
/// 流行页与分区页共用这一组装逻辑：增量时合并并给出新增条数，
/// 非增量时直接替换为新批次并给出普通刷新文案。
/// </remarks>
public class RecommendRefreshApplierTests
{
    private const string AddedTemplate = "已增量刷新，新增 {0} 条推荐视频";
    private const string ReloadedTemplate = "已刷新推荐视频";

    [Fact]
    public void Apply_IncrementalWithNewItems_ReportsAddedCount()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("x", "x"), new TestVideo("y", "y") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: true,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(new[] { "x", "y", "a" }, result.Items.Select(p => p.Id));
        Assert.Equal("已增量刷新，新增 2 条推荐视频", result.Message);
    }

    [Fact]
    public void Apply_IncrementalWithoutNewItems_FallsBackToReloadedText()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("a", "a-new") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: true,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(new[] { "a" }, result.Items.Select(p => p.Id));
        Assert.Equal(ReloadedTemplate, result.Message);
    }

    [Fact]
    public void Apply_Reloaded_ReturnsIncomingBatchWithReloadedText()
    {
        var current = new[] { new TestVideo("a", "a") };
        var incoming = new[] { new TestVideo("x", "x") };

        var result = RecommendRefreshApplier.Apply(
            current,
            incoming,
            p => p.Id,
            isIncremental: false,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(new[] { "x" }, result.Items.Select(p => p.Id));
        Assert.Equal(ReloadedTemplate, result.Message);
    }

    [Fact]
    public void Apply_NullIncoming_ProducesEmptyItemsWithReloadedText()
    {
        var current = new[] { new TestVideo("a", "a") };

        var result = RecommendRefreshApplier.Apply<TestVideo>(
            current,
            null,
            p => p.Id,
            isIncremental: false,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Empty(result.Items);
        Assert.Equal(ReloadedTemplate, result.Message);
    }

    private sealed record TestVideo(string Id, string Title);
}
