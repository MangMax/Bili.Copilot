// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 头像名称列表：默认横向排布并可左右滚动，展开后显示两列人员网格.
/// </summary>
/// <remarks>
/// 联合投稿场景下替代原来拼接成一段文字的投稿人姓名列表；
/// 条目类型固定为 <see cref="VideoContributorViewModel"/>，展开/滚动文案由调用方传入.
/// </remarks>
public sealed partial class ContributorListControl : UserControl
{
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(200);

    private readonly UISettings _uiSettings = new();
    private Storyboard? _transition;
    private bool _isExpanded;
    private bool _attached;

    /// <summary>
    /// <see cref="ItemsSource"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(ContributorListControl), new PropertyMetadata(default, OnItemsSourceChanged));

    /// <summary>
    /// <see cref="Header"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(ContributorListControl), new PropertyMetadata(default));

    /// <summary>
    /// <see cref="ExpandText"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty ExpandTextProperty =
        DependencyProperty.Register(nameof(ExpandText), typeof(string), typeof(ContributorListControl), new PropertyMetadata(string.Empty, OnExpandTextChanged));

    /// <summary>
    /// <see cref="CollapseText"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty CollapseTextProperty =
        DependencyProperty.Register(nameof(CollapseText), typeof(string), typeof(ContributorListControl), new PropertyMetadata(string.Empty, OnExpandTextChanged));

    /// <summary>
    /// <see cref="ScrollLeftText"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty ScrollLeftTextProperty =
        DependencyProperty.Register(nameof(ScrollLeftText), typeof(string), typeof(ContributorListControl), new PropertyMetadata(string.Empty));

    /// <summary>
    /// <see cref="ScrollRightText"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty ScrollRightTextProperty =
        DependencyProperty.Register(nameof(ScrollRightText), typeof(string), typeof(ContributorListControl), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Initializes a new instance of the <see cref="ContributorListControl"/> class.
    /// </summary>
    public ContributorListControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 条目集合，元素类型为 <see cref="VideoContributorViewModel"/>.
    /// </summary>
    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// 列表标题；为空时不显示标题.
    /// </summary>
    public string? Header
    {
        get => (string?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// 收起状态下的展开按钮文案.
    /// </summary>
    public string ExpandText
    {
        get => (string)GetValue(ExpandTextProperty);
        set => SetValue(ExpandTextProperty, value);
    }

    /// <summary>
    /// 展开状态下的收起按钮文案.
    /// </summary>
    public string CollapseText
    {
        get => (string)GetValue(CollapseTextProperty);
        set => SetValue(CollapseTextProperty, value);
    }

    /// <summary>
    /// 向左滚动按钮的提示与自动化名称.
    /// </summary>
    public string ScrollLeftText
    {
        get => (string)GetValue(ScrollLeftTextProperty);
        set => SetValue(ScrollLeftTextProperty, value);
    }

    /// <summary>
    /// 向右滚动按钮的提示与自动化名称.
    /// </summary>
    public string ScrollRightText
    {
        get => (string)GetValue(ScrollRightTextProperty);
        set => SetValue(ScrollRightTextProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ContributorListControl)d).ResetToListState();

    private static void OnExpandTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ContributorListControl)d).RefreshExpandLabel();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _attached = true;
        HorizontalHost.Opacity = 1;
        ExpandedHost.Opacity = 1;
        RefreshExpandLabel();
        UpdateExpandAvailability();
        UpdateScrollButtons();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _attached = false;
        _transition?.Stop();
        _transition = null;
    }

    private void ResetToListState()
    {
        _isExpanded = false;
        ApplyExpandedState(false);
        UpdateExpandAvailability();
        Scroller.ChangeView(0, null, null, true);
        UpdateScrollButtons();
    }

    private void UpdateExpandAvailability()
    {
        var count = ItemsSource is ICollection collection ? collection.Count : 0;
        ExpandButton.Visibility = count > 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyExpandedState(bool animate)
    {
        var previous = _transition;
        _transition = null;
        previous?.Stop();

        var fromHeight = ListViewport.ActualHeight;
        var canAnimate = animate && _attached && _uiSettings.AnimationsEnabled && fromHeight > 1;

        ScrollLeftButton.Visibility = Visibility.Collapsed;
        ScrollRightButton.Visibility = Visibility.Collapsed;

        var incoming = _isExpanded ? (FrameworkElement)ExpandedHost : HorizontalHost;
        var outgoing = _isExpanded ? (FrameworkElement)HorizontalHost : ExpandedHost;

        outgoing.Visibility = Visibility.Collapsed;
        incoming.Visibility = Visibility.Visible;
        Scroller.ChangeView(0, null, null, true);

        ExpandIcon.Symbol = _isExpanded ? FluentIcons.Common.Symbol.ChevronUp : FluentIcons.Common.Symbol.ChevronDown;
        RefreshExpandLabel();

        // 先测出目标高度，再回到当前高度做动画，避免展开瞬间内容跳动.
        ListViewport.Height = double.NaN;
        ListViewport.UpdateLayout();
        var toHeight = ListViewport.ActualHeight;
        incoming.Opacity = 1;
        if (!_isExpanded)
        {
            UpdateScrollButtons();
        }

        if (!canAnimate || Math.Abs(toHeight - fromHeight) < 0.5)
        {
            ListViewport.Height = double.NaN;
            if (!_isExpanded)
            {
                UpdateScrollButtons();
            }

            return;
        }

        ListViewport.Height = fromHeight;
        incoming.Opacity = 0;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var storyboard = new Storyboard();

        var heightAnimation = new DoubleAnimation
        {
            From = fromHeight,
            To = toHeight,
            Duration = TransitionDuration,
            EasingFunction = ease,
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(heightAnimation, ListViewport);
        Storyboard.SetTargetProperty(heightAnimation, "Height");
        storyboard.Children.Add(heightAnimation);

        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TransitionDuration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(opacityAnimation, incoming);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);

        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_transition, storyboard))
            {
                return;
            }

            ListViewport.Height = double.NaN;
            incoming.Opacity = 1;
            if (!_isExpanded)
            {
                UpdateScrollButtons();
            }

            _transition = null;
        };

        _transition = storyboard;
        storyboard.Begin();
    }

    private void RefreshExpandLabel()
    {
        var text = _isExpanded ? CollapseText : ExpandText;
        ExpandLabel.Text = text;
        AutomationProperties.SetName(ExpandButton, text);
        ToolTipService.SetToolTip(ExpandButton, string.IsNullOrWhiteSpace(text) ? null : text);
    }

    private void UpdateScrollButtons()
    {
        if (!_attached || Scroller is null || _isExpanded)
        {
            return;
        }

        var overflow = Scroller.ScrollableWidth > 1;
        var offset = Scroller.HorizontalOffset;
        ScrollLeftButton.Visibility = overflow && offset > 1 ? Visibility.Visible : Visibility.Collapsed;
        ScrollRightButton.Visibility = overflow && offset < Scroller.ScrollableWidth - 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnScrollerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e) => UpdateScrollButtons();

    private void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e) => UpdateScrollButtons();

    private void OnListViewportSizeChanged(object sender, SizeChangedEventArgs e)
        => ListViewport.Clip = new RectangleGeometry { Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        ApplyExpandedState(true);
    }

    private void OnScrollLeftClick(object sender, RoutedEventArgs e) => Scroll(-1);

    private void OnScrollRightClick(object sender, RoutedEventArgs e) => Scroll(1);

    private void Scroll(int direction)
    {
        if (Scroller is null)
        {
            return;
        }

        var step = Math.Max(176, Scroller.ViewportWidth * 0.9);
        var target = Math.Clamp(Scroller.HorizontalOffset + (direction * step), 0, Scroller.ScrollableWidth);
        Scroller.ChangeView(target, null, null, !_uiSettings.AnimationsEnabled);
    }
}