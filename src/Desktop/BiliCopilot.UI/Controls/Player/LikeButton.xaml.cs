// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.WinUIKernel.Share.Base;
using System.Windows.Input;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// 点赞按钮.
/// </summary>
public sealed partial class LikeButton : LayoutUserControlBase
{
    private bool _isTripleTipShown;

    /// <summary>
    /// <see cref="IsLiked"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty IsLikedProperty =
        DependencyProperty.Register(nameof(IsLiked), typeof(bool), typeof(LikeButton), new PropertyMetadata(default, OnIsLikedChanged));

    /// <summary>
    /// <see cref="IsCompactOverlay"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty IsCompactOverlayProperty =
        DependencyProperty.Register(nameof(IsCompactOverlay), typeof(bool), typeof(LikeButton), new PropertyMetadata(default, OnIsCompactOverlayChanged));

    /// <summary>
    /// <see cref="Tip"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty TipProperty =
        DependencyProperty.Register(nameof(Tip), typeof(string), typeof(LikeButton), new PropertyMetadata(default));

    /// <summary>
    /// <see cref="Command"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(LikeButton), new PropertyMetadata(default));

    /// <summary>
    /// <see cref="TripleCommand"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty TripleCommandProperty =
        DependencyProperty.Register(nameof(TripleCommand), typeof(ICommand), typeof(LikeButton), new PropertyMetadata(default));

    /// <summary>
    /// Initializes a new instance of the <see cref="LikeButton"/> class.
    /// </summary>
    public LikeButton() => InitializeComponent();

    /// <summary>
    /// 是否已点赞.
    /// </summary>
    public bool IsLiked
    {
        get => (bool)GetValue(IsLikedProperty);
        set => SetValue(IsLikedProperty, value);
    }

    /// <summary>
    /// 是否处于小窗模式.
    /// </summary>
    public bool IsCompactOverlay
    {
        get => (bool)GetValue(IsCompactOverlayProperty);
        set => SetValue(IsCompactOverlayProperty, value);
    }

    /// <summary>
    /// 提示文本.
    /// </summary>
    public string Tip
    {
        get => (string)GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    /// <summary>
    /// 命令.
    /// </summary>
    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// 一键三连命令.
    /// </summary>
    public ICommand TripleCommand
    {
        get => (ICommand)GetValue(TripleCommandProperty);
        set => SetValue(TripleCommandProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        CloseTripleTip();
        _isTripleTipShown = false;
    }

    private static void OnIsLikedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LikeButton button && e.NewValue is false)
        {
            button._isTripleTipShown = false;
        }
    }

    private static void OnIsCompactOverlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LikeButton button && e.NewValue is true)
        {
            button.CloseTripleTip();
        }
    }

    private void CloseTripleTip()
    {
        if (TripleTip.IsOpen)
        {
            TripleTip.IsOpen = false;
        }
    }

    private void OnLikeButtonClick(object sender, RoutedEventArgs e)
    {
        var canOpen = !IsLiked && !_isTripleTipShown && !IsCompactOverlay;
        CloseTripleTip();
        if (canOpen)
        {
            _isTripleTipShown = true;
            TripleTip.IsOpen = true;
        }
    }

    private void OnTripleButtonClick(TeachingTip sender, object args)
        => TripleTip.IsOpen = false;

    private void OnTripleTipClosed(TeachingTip sender, object args)
    {
        // 点赞命令失败时 IsLiked 仍为 false，下一次点击应能再次提示.
        if (!IsLiked)
        {
            _isTripleTipShown = false;
        }
    }
}
