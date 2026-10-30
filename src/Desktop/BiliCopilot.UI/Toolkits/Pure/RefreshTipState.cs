// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 刷新提示的状态.
/// </summary>
public enum RefreshTipStatus
{
    /// <summary>
    /// 不显示提示.
    /// </summary>
    Hidden,

    /// <summary>
    /// 刷新请求进行中.
    /// </summary>
    Refreshing,

    /// <summary>
    /// 刷新完成，只提供「撤销刷新」入口.
    /// </summary>
    Completed,

    /// <summary>
    /// 刷新失败，提示收起.
    /// </summary>
    Failed,
}

/// <summary>
/// 刷新提示的展示状态.
/// </summary>
/// <remarks>
/// 左下角浮层由「刷新中」与「刷新结果」共用同一个容器。
/// 刷新中展示进度与提示文案；刷新完成后不再展示结果文案，只保留「撤销刷新」入口，
/// 避免浮层长期占用一行文字空间。可见性与撤销入口都由状态派生。
/// </remarks>
public readonly record struct RefreshTipState
{
    private RefreshTipState(RefreshTipStatus status, string message, bool isProgressVisible, bool canUndo)
    {
        Status = status;
        Message = message;
        IsProgressVisible = isProgressVisible;
        CanUndo = canUndo;
    }

    /// <summary>
    /// 不显示提示.
    /// </summary>
    public static RefreshTipState Hidden { get; } = new(RefreshTipStatus.Hidden, string.Empty, false, false);

    /// <summary>
    /// 刷新失败，提示收起.
    /// </summary>
    public static RefreshTipState Failed { get; } = new(RefreshTipStatus.Failed, string.Empty, false, false);

    /// <summary>
    /// 刷新完成，只提供撤销入口.
    /// </summary>
    public static RefreshTipState Completed { get; } = new(RefreshTipStatus.Completed, string.Empty, false, true);

    /// <summary>
    /// 当前状态.
    /// </summary>
    public RefreshTipStatus Status { get; }

    /// <summary>
    /// 提示文案；只有刷新中才有文案，完成与失败态为空.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 是否显示进度环.
    /// </summary>
    public bool IsProgressVisible { get; }

    /// <summary>
    /// 是否提供「撤销刷新」入口.
    /// </summary>
    public bool CanUndo { get; }

    /// <summary>
    /// 浮层是否可见.
    /// </summary>
    public bool IsVisible => Status is RefreshTipStatus.Refreshing or RefreshTipStatus.Completed;

    /// <summary>
    /// 是否展示提示文案.
    /// </summary>
    public bool IsMessageVisible => Status is RefreshTipStatus.Refreshing;

    /// <summary>
    /// 创建「刷新中」状态.
    /// </summary>
    /// <param name="message">加载文案.</param>
    /// <returns>刷新中状态.</returns>
    public static RefreshTipState Refreshing(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new(RefreshTipStatus.Refreshing, message, true, false);
    }
}
