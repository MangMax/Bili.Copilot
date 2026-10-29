// Copyright (c) Bili Copilot. All rights reserved.

using System.Windows.Input;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 刷新提示浮层，锚定在内容区左下角，刷新时展示进度，完成后只提供「撤销刷新」入口.
/// </summary>
/// <remarks>
/// 流行页与分区页复用同一控件。页面只提供状态、文案与撤销命令，
/// 控件内部根据状态切换进度环、提示文案与撤销按钮，容器本身不占用列表布局空间。
/// </remarks>
public sealed partial class RefreshResultTipControl : UserControl
{
    /// <summary>
    /// <see cref="State"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(RefreshTipStatus), typeof(RefreshResultTipControl), new PropertyMetadata(RefreshTipStatus.Hidden));

    /// <summary>
    /// <see cref="Message"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(RefreshResultTipControl), new PropertyMetadata(string.Empty));

    /// <summary>
    /// <see cref="UndoCommand"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty UndoCommandProperty =
        DependencyProperty.Register(nameof(UndoCommand), typeof(ICommand), typeof(RefreshResultTipControl), new PropertyMetadata(default));

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshResultTipControl"/> class.
    /// </summary>
    public RefreshResultTipControl() => InitializeComponent();

    /// <summary>
    /// 提示状态.
    /// </summary>
    public RefreshTipStatus State
    {
        get => (RefreshTipStatus)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// 提示文案.
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// 撤销刷新命令.
    /// </summary>
    public ICommand? UndoCommand
    {
        get => (ICommand?)GetValue(UndoCommandProperty);
        set => SetValue(UndoCommandProperty, value);
    }

    /// <summary>
    /// 根据状态取得浮层可见性.
    /// </summary>
    /// <param name="state">提示状态.</param>
    /// <returns>刷新中与刷新完成可见，其余折叠.</returns>
    public static Visibility GetVisibility(RefreshTipStatus state)
        => state is RefreshTipStatus.Refreshing or RefreshTipStatus.Completed ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// 根据宿主宽度计算浮层最大宽度.
    /// </summary>
    /// <param name="availableWidth">宿主控件的实际宽度.</param>
    /// <returns>扣掉左右安全边距后的宽度；宿主尚未完成布局时不做限制.</returns>
    /// <remarks>
    /// 小窗口下浮层不能越过内容区右边界，否则文字会被截断；首次布局前宽度为 0，此时不限制。
    /// </remarks>
    public static double GetMaxWidth(double availableWidth)
        => double.IsFinite(availableWidth) && availableWidth > 0
            ? Math.Max(0, availableWidth - 32)
            : double.PositiveInfinity;

    /// <summary>
    /// 是否显示进度环.
    /// </summary>
    /// <param name="state">提示状态.</param>
    /// <returns>加载态返回 <c>true</c>.</returns>
    public static bool IsProgressVisible(RefreshTipStatus state) => state == RefreshTipStatus.Refreshing;

    /// <summary>
    /// 是否显示提示文案.
    /// </summary>
    /// <param name="state">提示状态.</param>
    /// <returns>只有刷新中返回 <c>true</c>；完成后不再展示结果文案.</returns>
    public static bool IsMessageVisible(RefreshTipStatus state) => state == RefreshTipStatus.Refreshing;

    /// <summary>
    /// 是否显示「撤销刷新」入口.
    /// </summary>
    /// <param name="state">提示状态.</param>
    /// <returns>刷新完成后返回 <c>true</c>；刷新中与失败态不提供撤销.</returns>
    public static bool IsUndoVisible(RefreshTipStatus state) => state == RefreshTipStatus.Completed;

    /// <summary>
    /// 进度环的可见性.
    /// </summary>
    /// <param name="state">提示状态.</param>
    /// <returns>加载态可见.</returns>
    public static Visibility GetProgressVisibility(RefreshTipStatus state)
        => IsProgressVisible(state) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// 提示文案的可见性.
    /// </summary>
    /// <param name="state">提示状态.</param>
    /// <returns>刷新中可见.</returns>
    public static Visibility GetMessageVisibility(RefreshTipStatus state)
        => IsMessageVisible(state) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// 「撤销刷新」入口的可见性.
    /// </summary>
    /// <param name="state">提示状态.</param>
    /// <returns>刷新完成后可见.</returns>
    public static Visibility GetUndoVisibility(RefreshTipStatus state)
        => IsUndoVisible(state) ? Visibility.Visible : Visibility.Collapsed;
}
