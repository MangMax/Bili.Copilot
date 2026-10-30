// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 刷新提示状态的展示规则。
/// </summary>
/// <remarks>
/// 左下角浮层由同一个控件承担「刷新中」与「刷新结果」两种形态，
/// 因此视图模型只需要一个状态，可见性、进度环、撤销入口都由该状态派生。
/// 刷新中展示进度与文案；刷新完成后不再展示结果文案，只保留「撤销刷新」入口；
/// 失败态直接收起，不提供撤销。
/// </remarks>
public class RefreshTipStateTests
{
    private const string RefreshingTemplate = "正在刷新推荐视频";

    [Fact]
    public void Hidden_IsNotVisibleAndOffersNoUndo()
    {
        var state = RefreshTipState.Hidden;

        Assert.Equal(RefreshTipStatus.Hidden, state.Status);
        Assert.False(state.IsVisible);
        Assert.False(state.IsProgressVisible);
        Assert.False(state.IsMessageVisible);
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
        Assert.False(state.IsMessageVisible);
        Assert.False(state.CanUndo);
        Assert.Equal(string.Empty, state.Message);
    }

    [Fact]
    public void Refreshing_ShowsProgressAndMessageButNoUndo()
    {
        var state = RefreshTipState.Refreshing(RefreshingTemplate);

        Assert.Equal(RefreshTipStatus.Refreshing, state.Status);
        Assert.True(state.IsVisible);
        Assert.True(state.IsProgressVisible);
        Assert.True(state.IsMessageVisible);
        Assert.False(state.CanUndo);
        Assert.Equal(RefreshingTemplate, state.Message);
    }

    [Fact]
    public void Completed_OffersUndoOnlyWithoutMessageOrProgress()
    {
        var state = RefreshTipState.Completed;

        Assert.Equal(RefreshTipStatus.Completed, state.Status);
        Assert.True(state.IsVisible);
        Assert.False(state.IsProgressVisible);
        Assert.False(state.IsMessageVisible);
        Assert.True(state.CanUndo);
        Assert.Equal(string.Empty, state.Message);
    }

    [Fact]
    public void Refreshing_MissingTemplate_Throws()
        => Assert.Throws<ArgumentNullException>(() => RefreshTipState.Refreshing(null!));
}
