// Copyright (c) Bili Copilot. All rights reserved.

using System.Windows.Input;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 刷新结果提示条，展示本次刷新的结果并提供「撤销刷新」入口.
/// </summary>
/// <remarks>
/// 流行页与分区页复用同一控件，页面只负责提供可见性、提示文案与撤销命令。
/// </remarks>
public sealed partial class RefreshResultTipControl : UserControl
{
    /// <summary>
    /// <see cref="IsOpen"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(RefreshResultTipControl), new PropertyMetadata(false));

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
    /// 是否显示提示条.
    /// </summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
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
}
