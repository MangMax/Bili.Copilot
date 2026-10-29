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
    /// 增量刷新完成，且取到了新增条目.
    /// </summary>
    Added,

    /// <summary>
    /// 刷新完成但没有新增条目，或本次是普通重载.
    /// </summary>
    Reloaded,

    /// <summary>
    /// 刷新失败，提示收起.
    /// </summary>
    Failed,
}

/// <summary>
/// 刷新提示的展示状态.
/// </summary>
/// <remarks>
/// 左下角浮层由「刷新中」与「刷新结果」共用同一个容器，
/// 可见性、进度环与撤销入口都由状态派生，页面只负责设置状态与文案。
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
    /// 当前状态.
    /// </summary>
    public RefreshTipStatus Status { get; }

    /// <summary>
    /// 提示文案.
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
    public bool IsVisible => Status is RefreshTipStatus.Refreshing or RefreshTipStatus.Added or RefreshTipStatus.Reloaded;

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

    /// <summary>
    /// 根据一次推荐刷新结果创建结果状态.
    /// </summary>
    /// <param name="isIncremental">本次刷新是否为增量刷新.</param>
    /// <param name="addedCount">本次刷新新增的条目数.</param>
    /// <param name="addedTemplate">带 <c>{0}</c> 占位符的增量刷新文案模板.</param>
    /// <param name="reloadedTemplate">普通（清空重载）刷新文案.</param>
    /// <returns>刷新结果状态.</returns>
    public static RefreshTipState FromRefreshResult(
        bool isIncremental,
        int addedCount,
        string addedTemplate,
        string reloadedTemplate)
    {
        var message = RefreshResultMessageBuilder.Build(
            isIncremental,
            addedCount,
            addedTemplate,
            reloadedTemplate);
        var status = isIncremental && addedCount > 0
            ? RefreshTipStatus.Added
            : RefreshTipStatus.Reloaded;

        return new(status, message, false, true);
    }
}
