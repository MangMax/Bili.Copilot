// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Automation;
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
    private readonly UISettings _uiSettings = new();
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
        RefreshExpandLabel();
        UpdateExpandAvailability();
        UpdateScrollButtons();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _attached = false;

    private void ResetToListState()
    {
        _isExpanded = false;
        ApplyExpandedState();
        UpdateExpandAvailability();
        Scroller?.ChangeView(0, null, null, true);
        UpdateScrollButtons();
    }

    private void UpdateExpandAvailability()
    {
        var count = ItemsSource is ICollection collection ? collection.Count : 0;
        ExpandButton.Visibility = count > 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyExpandedState()
    {
        HorizontalHost.Visibility = _isExpanded ? Visibility.Collapsed : Visibility.Visible;
        ExpandedHost.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandIcon.Symbol = _isExpanded ? FluentIcons.Common.Symbol.ChevronUp : FluentIcons.Common.Symbol.ChevronDown;
        if (_isExpanded)
        {
            ScrollLeftButton.Visibility = Visibility.Collapsed;
            ScrollRightButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            DispatcherQueue.TryEnqueue(UpdateScrollButtons);
        }

        RefreshExpandLabel();
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

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        ApplyExpandedState();
        if (!_isExpanded)
        {
            UpdateScrollButtons();
        }
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