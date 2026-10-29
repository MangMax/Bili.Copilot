// Copyright (c) Bili Copilot. All rights reserved.

using System.Windows.Input;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Media.Animation;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 刷新提示浮层，居中显示，刷新时展示进度，完成后只提供「撤销刷新」入口.
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
        DependencyProperty.Register(nameof(State), typeof(RefreshTipStatus), typeof(RefreshResultTipControl), new PropertyMetadata(RefreshTipStatus.Hidden, OnStateChanged));

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
    /// <see cref="DismissCommand"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty DismissCommandProperty =
        DependencyProperty.Register(nameof(DismissCommand), typeof(ICommand), typeof(RefreshResultTipControl), new PropertyMetadata(default));

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshResultTipControl"/> class.
    /// </summary>
    public RefreshResultTipControl() => InitializeComponent();

    /// <summary>
    /// 状态变化时播放一次内容淡入.
    /// </summary>
    /// <param name="d">依赖对象.</param>
    /// <param name="e">变化参数.</param>
    /// <remarks>
    /// 刷新中与完成态的容器可见性都是可见，Implicit 显示动画不会触发，
    /// 因此「正在刷新」变成「撤销刷新」时由这里补一段短淡入，避免内容直接跳变。
    /// </remarks>
    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RefreshResultTipControl control || control.ContentRoot is null)
        {
            return;
        }

        if (e.OldValue is RefreshTipStatus.Hidden || e.NewValue is RefreshTipStatus.Hidden or RefreshTipStatus.Failed)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            From = 0.35,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
        };
        Storyboard.SetTarget(animation, control.ContentRoot);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

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
    /// 状态切换时用于播放淡入的内容容器.
    /// </summary>
    private FrameworkElement? ContentRoot => FindName("TipContent") as FrameworkElement;

    /// <summary>
    /// 手动关闭提示的命令.
    /// </summary>
    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
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
    /// 撤销图标的可见性.
    /// </summary>
    /// <param name="state">提示状态.</param>
    /// <returns>刷新完成后可见.</returns>
    public static Visibility GetUndoIconVisibility(RefreshTipStatus state)
        => IsUndoVisible(state) ? Visibility.Visible : Visibility.Collapsed;

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
