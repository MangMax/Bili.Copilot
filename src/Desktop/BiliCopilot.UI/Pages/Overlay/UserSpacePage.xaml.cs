// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.WinUIKernel.Share.Base;
using System.ComponentModel;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 用户空间页面.
/// </summary>
public sealed partial class UserSpacePage : UserSpacePageBase, IParameterPage, ICancelPageViewModel
{
    private int _loadGeneration = -1;
    private ScrollView? _activeScroll;
    private FrameworkElement? _scrollContent;
    private ScrollBar? _verticalScrollBar;
    private Thickness _scrollBarMargin;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSpacePage"/> class.
    /// </summary>
    public UserSpacePage()
    {
        InitializeComponent();
        Loaded += OnSpaceLoaded;
        Unloaded += OnSpaceUnloaded;
    }

    private void OnSpaceLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        QueueScrollHostUpdate();
    }

    private void OnSpaceUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        LayoutUpdated -= OnSpaceLayoutUpdated;
        DisconnectScrollBar();
        SpaceHeader.ConnectScroll(null);
        _activeScroll = null;
        _scrollContent = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UserSpacePageViewModel.SelectedSection) or nameof(UserSpacePageViewModel.IsSearchMode))
        {
            QueueScrollHostUpdate();
        }
    }

    private void QueueScrollHostUpdate()
    {
        // 等待分区模板完成布局后绑定一次，避免滚动时反复遍历视觉树。
        LayoutUpdated -= OnSpaceLayoutUpdated;
        LayoutUpdated += OnSpaceLayoutUpdated;
    }

    private void OnSpaceLayoutUpdated(object? sender, object e)
    {
        var root = ViewModel.IsSearchMode ? (DependencyObject)SearchSection : SpaceBody;
        var host = FindChild<IncrementVerticalScrollControl>(root);
        var scroll = host is null ? null : FindChild<ScrollView>(host);
        if (scroll is null)
        {
            return;
        }

        LayoutUpdated -= OnSpaceLayoutUpdated;
        if (scroll == _activeScroll)
        {
            return;
        }

        DisconnectScrollBar();
        _activeScroll = scroll;
        _scrollContent = host?.Element as FrameworkElement;
        scroll.ApplyTemplate();
        ConnectScrollBar();
        UpdateScrollInset();
    }

    private void ConnectScrollBar()
    {
        if (_activeScroll is null || _verticalScrollBar is not null)
        {
            return;
        }

        _verticalScrollBar = FindChild<ScrollBar>(_activeScroll, "PART_VerticalScrollBar");
        if (_verticalScrollBar is not null)
        {
            _scrollBarMargin = _verticalScrollBar.Margin;
            _activeScroll.ViewChanged += OnScrollViewChanged;
            UpdateScrollBarInset();
        }
    }

    private void DisconnectScrollBar()
    {
        if (_activeScroll is not null)
        {
            _activeScroll.ViewChanged -= OnScrollViewChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.Margin = _scrollBarMargin;
            _verticalScrollBar = null;
        }
    }

    private void OnScrollViewChanged(ScrollView sender, object e) => UpdateScrollBarInset();

    private void UpdateScrollBarInset()
    {
        if (_verticalScrollBar is null || _activeScroll is null)
        {
            return;
        }

        // 仅缩短滚动条轨道，避免改变内容视口导致锚点跳动。
        var collapsed = Math.Clamp(_activeScroll.VerticalOffset, 0, SpaceHeader.CollapseHeight);
        var inset = Math.Max(0, SpaceHeader.ActualHeight - collapsed);
        var margin = new Thickness(_scrollBarMargin.Left, _scrollBarMargin.Top + inset,
            _scrollBarMargin.Right, _scrollBarMargin.Bottom);
        if (_verticalScrollBar.Margin != margin)
        {
            _verticalScrollBar.Margin = margin;
        }
    }

    private void OnSpaceHeaderSizeChanged(object sender, SizeChangedEventArgs e) => UpdateScrollInset();

    private void UpdateScrollInset()
    {
        if (_scrollContent is not null)
        {
            _scrollContent.Margin = new Thickness(0, SpaceHeader.ActualHeight + 16, 0, 16);
            _scrollContent.MinHeight = Math.Max(160, PageLayout.ActualHeight - SpaceHeader.ActualHeight - 32);
        }

        SpaceHeader.ConnectScroll(_activeScroll);
        UpdateScrollBarInset();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PageLayout.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        UpdateScrollInset();
    }

    private void OnHeaderPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_activeScroll is not null)
        {
            _activeScroll.ScrollBy(0, -e.GetCurrentPoint(this).Properties.MouseWheelDelta,
                new ScrollingScrollOptions(ScrollingAnimationMode.Auto, ScrollingSnapPointsMode.Ignore));
            e.Handled = true;
        }
    }

    private static T? FindChild<T>(DependencyObject root, string? name = null)
        where T : DependencyObject
    {
        if (root is T match && (name is null || (root is FrameworkElement element && element.Name == name)))
        {
            return match;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindChild<T>(VisualTreeHelper.GetChild(root, i), name) is { } child)
            {
                return child;
            }
        }

        return null;
    }

    public void SetParameter(object? parameter)
    {
        if (parameter is UserProfile profile)
        {
            // 叠层按类型缓存页面，同一页面会再次收到新的用户参数。
            ViewModel.InitializeCommand.Execute(profile);

            // 记录本次页面发起的加载代次，卸载时只取消这一代，不影响新页面。
            _loadGeneration = ViewModel.CurrentLoadGeneration;
            ViewModel.InitializeUserInformationCommand.Execute(default);
        }
    }

    public void CancelLoading()
        => CancelPageLoad();

    protected override void OnPageUnloaded()
        => CancelPageLoad();

    private void CancelPageLoad()
    {
        // 只有真正取消了本页记录的那一代，才把记录刷新为新的当前代次：
        // 返回页面后再次切走/关闭仍能取消新发起的加载，而旧页面的过期记录
        // 不会误伤之后发起的新加载。
        if (ViewModel.CancelLoad(_loadGeneration))
        {
            _loadGeneration = ViewModel.CurrentLoadGeneration;
        }
    }
}

/// <summary>
/// 用户空间页面基类.
/// </summary>
public abstract class UserSpacePageBase : LayoutPageBase<UserSpacePageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserSpacePageBase"/> class.
    /// </summary>
    protected UserSpacePageBase()
    {
        ViewModel = this.Get<UserSpacePageViewModel>();
    }
}
