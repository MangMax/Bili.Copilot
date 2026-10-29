// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 刷新结果提示文案的组装规则。
/// </summary>
/// <remarks>
/// 提示文案同时承载「本次新增了多少条」与「撤销刷新」入口，
/// 因此增量刷新拿到 0 条新增时仍要给出提示（否则用户失去撤销入口），
/// 只是不出现「新增」字样。
/// </remarks>
public class RefreshResultMessageBuilderTests
{
    private const string AddedTemplate = "已增量刷新，新增 {0} 条推荐视频";
    private const string ReloadedTemplate = "已刷新推荐视频";

    [Fact]
    public void Build_IncrementalWithNewItems_ReportsTheAddedCount()
    {
        var message = RefreshResultMessageBuilder.Build(
            isIncremental: true,
            addedCount: 3,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal("已增量刷新，新增 3 条推荐视频", message);
    }

    [Fact]
    public void Build_IncrementalWithoutNewItems_FallsBackToReloadedText()
    {
        var message = RefreshResultMessageBuilder.Build(
            isIncremental: true,
            addedCount: 0,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(ReloadedTemplate, message);
        Assert.DoesNotContain("新增", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ReloadedRefresh_UsesReloadedText()
    {
        var message = RefreshResultMessageBuilder.Build(
            isIncremental: false,
            addedCount: 0,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(ReloadedTemplate, message);
    }

    [Fact]
    public void Build_ReloadedRefreshIgnoresAddedCount()
    {
        var message = RefreshResultMessageBuilder.Build(
            isIncremental: false,
            addedCount: 5,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(ReloadedTemplate, message);
    }

    [Fact]
    public void Build_NegativeAddedCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RefreshResultMessageBuilder.Build(
            isIncremental: true,
            addedCount: -1,
            AddedTemplate,
            ReloadedTemplate));
    }

    [Fact]
    public void Build_MissingTemplate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RefreshResultMessageBuilder.Build(
            isIncremental: true,
            addedCount: 1,
            null!,
            ReloadedTemplate));

        Assert.Throws<ArgumentNullException>(() => RefreshResultMessageBuilder.Build(
            isIncremental: false,
            addedCount: 0,
            AddedTemplate,
            null!));
    }
}
