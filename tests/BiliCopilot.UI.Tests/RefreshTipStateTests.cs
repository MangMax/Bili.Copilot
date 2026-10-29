// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 刷新提示状态的展示规则。
/// </summary>
/// <remarks>
/// 左下角浮层由同一个控件承担「刷新中」与「刷新结果」两种形态，
/// 因此视图模型只需要一个状态，可见性、进度环、撤销入口都由该状态派生。
/// 失败态直接收起，不展示「已刷新」文案，也不提供撤销。
/// </remarks>
public class RefreshTipStateTests
{
    private const string RefreshingTemplate = "正在刷新推荐视频";
    private const string AddedTemplate = "已增量刷新，新增 {0} 条推荐视频";
    private const string ReloadedTemplate = "已刷新推荐视频";

    [Fact]
    public void Hidden_IsNotVisibleAndOffersNoUndo()
    {
        var state = RefreshTipState.Hidden;

        Assert.Equal(RefreshTipStatus.Hidden, state.Status);
        Assert.False(state.IsVisible);
        Assert.False(state.IsProgressVisible);
        Assert.False(state.CanUndo);
        Assert.Equal(string.Empty, state.Message);
    }

    [Fact]
    public void Failed_CollapsesWithoutMessageOrUndo()
    {
        var state = RefreshTipState.Failed;

        Assert.Equal(RefreshTipStatus.Failed, state.Status);
        Assert.False(state.IsVisible);
        Assert.False(state.IsProgressVisible);
        Assert.False(state.CanUndo);
        Assert.Equal(string.Empty, state.Message);
    }

    [Fact]
    public void Refreshing_ShowsProgressAndDoesNotOfferUndo()
    {
        var state = RefreshTipState.Refreshing(RefreshingTemplate);

        Assert.Equal(RefreshTipStatus.Refreshing, state.Status);
        Assert.True(state.IsVisible);
        Assert.True(state.IsProgressVisible);
        Assert.False(state.CanUndo);
        Assert.Equal(RefreshingTemplate, state.Message);
    }

    [Fact]
    public void FromRefreshResult_IncrementalWithNewItems_ReportsAddedCountAndOffersUndo()
    {
        var state = RefreshTipState.FromRefreshResult(
            isIncremental: true,
            addedCount: 3,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(RefreshTipStatus.Added, state.Status);
        Assert.True(state.IsVisible);
        Assert.False(state.IsProgressVisible);
        Assert.True(state.CanUndo);
        Assert.Equal("已增量刷新，新增 3 条推荐视频", state.Message);
    }

    [Fact]
    public void FromRefreshResult_IncrementalWithoutNewItems_FallsBackToReloadedAndOffersUndo()
    {
        var state = RefreshTipState.FromRefreshResult(
            isIncremental: true,
            addedCount: 0,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(RefreshTipStatus.Reloaded, state.Status);
        Assert.True(state.IsVisible);
        Assert.False(state.IsProgressVisible);
        Assert.True(state.CanUndo);
        Assert.Equal(ReloadedTemplate, state.Message);
        Assert.DoesNotContain("新增", state.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromRefreshResult_NonIncremental_UsesReloadedTextAndOffersUndo()
    {
        var state = RefreshTipState.FromRefreshResult(
            isIncremental: false,
            addedCount: 0,
            AddedTemplate,
            ReloadedTemplate);

        Assert.Equal(RefreshTipStatus.Reloaded, state.Status);
        Assert.True(state.IsVisible);
        Assert.True(state.CanUndo);
        Assert.Equal(ReloadedTemplate, state.Message);
    }

    [Fact]
    public void Refreshing_MissingTemplate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RefreshTipState.Refreshing(null!));
    }

    [Fact]
    public void FromRefreshResult_NegativeAddedCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RefreshTipState.FromRefreshResult(
            isIncremental: true,
            addedCount: -1,
            AddedTemplate,
            ReloadedTemplate));
    }
}
