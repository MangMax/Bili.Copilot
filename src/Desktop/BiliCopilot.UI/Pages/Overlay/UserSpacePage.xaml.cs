// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Moment;
using BiliCopilot.UI.Toolkits;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.WinUIKernel.Share.Base;
using System.ComponentModel;
using System.Numerics;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 用户空间页面.
/// </summary>
public sealed partial class UserSpacePage : UserSpacePageBase, IParameterPage, ICancelPageViewModel, IRetainedOverlayPage
{
    private const int MaxAvatarStartAttempts = 60;

    private readonly UISettings _uiSettings = new();
    private int _loadGeneration = -1;
    private ScrollView? _activeScroll;
    private IncrementVerticalScrollControl? _activeScrollHost;
    private UserSpacePageHeader? _activeHeader;
    private UserProfile? _navigationProfile;
    private const int MaxSkeletonFillRounds = 30;

    private UserMomentDetailViewModel? _momentSection;
    private bool _videoSkeletonGaveUp;

    /// <summary>
    /// 视频网格实际渲染的条目：真实视频 + 补齐视口预算的骨架占位.
    /// </summary>
    public ObservableCollection<object> DisplayVideos { get; } = new();
    private int _skeletonFillRounds;
    private int _avatarStartAttempts;
    private bool _avatarStartInProgress;

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
        UpdateBackdropColor();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SubmissionNav.SelectionChanged += OnSubmissionSelectionChanged;
        SubmissionNavOverlayView.SelectionChanged += OnSubmissionSelectionChanged;
        MomentFilterNav.SelectionChanged += OnMomentFilterSelectionChanged;
        MomentFilterNavOverlay.SelectionChanged += OnMomentFilterSelectionChanged;
        SyncMomentFilterSelection();
        AttachMomentSection();
        QueueSubmissionSelectionSync();
        QueueScrollHostUpdate();
    }

    private void OnSpaceUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        SubmissionNav.SelectionChanged -= OnSubmissionSelectionChanged;
        SubmissionNavOverlayView.SelectionChanged -= OnSubmissionSelectionChanged;
        MomentFilterNav.SelectionChanged -= OnMomentFilterSelectionChanged;
        MomentFilterNavOverlay.SelectionChanged -= OnMomentFilterSelectionChanged;
        AttachMomentSection();
        ViewModel.VideoFeed.Items.CollectionChanged -= OnVideoFeedChanged;
        LayoutUpdated -= OnSpaceLayoutUpdated;
        CancelAvatarTransition();
        DisconnectHeader();
        _activeScroll = null;
        ConnectBackdrop();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UserSpacePageViewModel.SelectedSection) or nameof(UserSpacePageViewModel.IsSearchMode))
        {
            QueueScrollHostUpdate();
        }
        else if (e.PropertyName is nameof(UserSpacePageViewModel.SelectedSubmissionTab))
        {
            QueueSubmissionSelectionSync();
        }
        else if (e.PropertyName is nameof(UserSpacePageViewModel.SelectedMomentFilter))
        {
            SyncMomentFilterSelection();
            _skeletonFillRounds = 0;
            _videoSkeletonGaveUp = false;
            QueueContentFillCheck();
            SyncVideoDisplay();
        }
        else if (e.PropertyName is nameof(UserSpacePageViewModel.MomentSection))
        {
            AttachMomentSection();
        }
        else if (e.PropertyName is nameof(UserSpacePageViewModel.IsMomentsTab))
        {
            _skeletonFillRounds = 0;
            _videoSkeletonGaveUp = false;
            QueueContentFillCheck();
            SyncVideoDisplay();
        }
        else if (e.PropertyName is nameof(UserSpacePageViewModel.IsVideoMomentFilter))
        {
            SyncVideoDisplay();
        }
        else if (e.PropertyName is nameof(UserSpacePageViewModel.IsSubmissionTab))
        {
            UpdateSubmissionOverlay();
        }
    }

    private void AttachMomentSection()
    {
        if (!ReferenceEquals(_momentSection, ViewModel.MomentSection))
        {
            if (_momentSection is not null)
            {
                _momentSection.ListUpdated -= OnMomentSectionListUpdated;
            }

            _momentSection = ViewModel.MomentSection;
            if (_momentSection is not null)
            {
                _momentSection.ListUpdated += OnMomentSectionListUpdated;
            }
        }

    }

    private void OnMomentSectionListUpdated(object? sender, EventArgs e) => QueueContentFillCheck();

    /// <summary>
    /// 列表更新后重新按视口判断：内容不够高时继续取下一页，
    /// 否则矮内容会停在首屏、整个页面也没有滚动余量。
    /// </summary>
    private void QueueContentFillCheck()
    {
        // 筛选后列表可能比视口矮：补取几页把首屏填满，但设上限避免一次筛选连续翻太多页。
        if (_activeScrollHost is not { } host)
        {
            return;
        }

        if (_skeletonFillRounds >= MaxSkeletonFillRounds)
        {
            // 补了很多页仍然填不满：收掉骨架，让列表按实际内容结束.
            if (!_videoSkeletonGaveUp)
            {
                _videoSkeletonGaveUp = true;
                SyncVideoDisplay();
            }

            return;
        }

        _skeletonFillRounds++;
        _ = host.DelayCheckItemsAsync();
    }

    private void OnVideoFeedChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncVideoDisplay();

    private void OnScrollHostBudgetChanged(int budget) => SyncVideoDisplay();

    /// <summary>
    /// 视频网格按视口预算铺骨架：先按一屏的数量占位，真实卡片到达后逐格替换。
    /// </summary>
    private void SyncVideoDisplay()
    {
        var host = _activeScrollHost;
        if (!ViewModel.IsMomentsTab || !ViewModel.IsVideoMomentFilter)
        {
            ResetVideoSlotBudget();
            return;
        }

        var count = ViewModel.VideoFeed.Items.Count;
        if (count == 0 && ViewModel.VideoFeed.IsEmpty)
        {
            VideoListDisplay.Apply(DisplayVideos, 0, _ => null);
            ResetVideoSlotBudget();
            return;
        }

        // 没有下一页时不再留骨架，避免占位永远等不到卡片.
        var canLoadMore = ViewModel.VideoFeed.HasMore && !_videoSkeletonGaveUp;
        var budget = host is { CurrentBudget: > 0 } ? host.CurrentBudget : VideoListDisplay.InitialSkeletonEstimate;
        var desired = canLoadMore ? Math.Max(count, Math.Min(budget, ScrollSlotBudget.DefaultMaxSlots)) : count;
        VideoListDisplay.Apply(DisplayVideos, desired, i => i < count ? ViewModel.VideoFeed.Items[i] : null);
        if (host is not null)
        {
            host.ItemCount = count;
            // 还有骨架时只按预算判断续页，骨架填满后恢复按滚动余量判断.
            host.ExtentFallbackEnabled = desired <= count;
        }

        // 骨架还在等卡片时主动续页：筛选刚切换时页面还是上一份内容的高度，
        // 滚动余量判据不会触发，这里按预算补一页.
        if (desired > count)
        {
            QueueContentFillCheck();
        }
    }

    private void ResetVideoSlotBudget()
    {
        if (_activeScrollHost is { } host)
        {
            host.ItemCount = -1;
            host.ExtentFallbackEnabled = true;
        }
    }

    private void SyncMomentFilterSelection()
    {
        if (!IsLoaded
            || ViewModel.MomentFilterTabs is not { Count: > 0 } tabs
            || ViewModel.SelectedMomentFilter is not { } selected)
        {
            return;
        }

        var index = tabs.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        if (!ReferenceEquals(MomentFilterNav.SelectedItem, selected))
        {
            MomentFilterNav.Select(index);
        }

        if (!ReferenceEquals(MomentFilterNavOverlay.SelectedItem, selected))
        {
            MomentFilterNavOverlay.Select(index);
        }
    }

    private void OnMomentFilterSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is UserSpaceTabViewModel tab && !ReferenceEquals(tab, ViewModel.SelectedMomentFilter))
        {
            ViewModel.SelectMomentFilterCommand.Execute(tab);
        }
    }

    private void OnSubmissionSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is UserSpaceTabViewModel tab && !ReferenceEquals(tab, ViewModel.SelectedSubmissionTab))
        {
            ViewModel.SelectSubmissionTabCommand.Execute(tab);
        }
    }

    private void QueueSubmissionSelectionSync()
        => DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, SyncSubmissionSelection);

    private void SyncSubmissionSelection()
    {
        if (!IsLoaded || ViewModel.SubmissionTabs is not { Count: > 0 } tabs || ViewModel.SelectedSubmissionTab is not { } selected)
        {
            return;
        }

        var index = tabs.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        if (!ReferenceEquals(SubmissionNav.SelectedItem, selected))
        {
            SubmissionNav.Select(index);
        }

        if (!ReferenceEquals(SubmissionNavOverlayView.SelectedItem, selected))
        {
            SubmissionNavOverlayView.Select(index);
        }
    }

    /// <summary>
    /// 投稿分类的图标：侧栏比纯文字更容易扫读.
    /// </summary>
    /// <param name="kind">分区类型.</param>
    /// <returns>图标.</returns>
    public static FluentIcons.Common.Symbol GetSubmissionIcon(UserSpaceTabKind kind)
        => kind switch
        {
            UserSpaceTabKind.Articles => FluentIcons.Common.Symbol.DocumentBulletList,
            UserSpaceTabKind.MomentPosts => FluentIcons.Common.Symbol.BubbleMultiple,
            UserSpaceTabKind.AllMoments => FluentIcons.Common.Symbol.TextBulletList,
            UserSpaceTabKind.Audio => FluentIcons.Common.Symbol.MusicNote2,
            UserSpaceTabKind.Collections => FluentIcons.Common.Symbol.Collections,
            _ => FluentIcons.Common.Symbol.VideoClip,
        };

    /// <summary>
    /// 分类栏滚出视口后，用固定在页面左上角的同一组分类接替；
    /// 两个入口只是同一个列表的两种呈现，不做任何位移计算。
    /// </summary>
    private void UpdateSubmissionOverlay()
    {
        var pinned = false;
        if (_activeScroll is not null
            && SectionHost.ActualHeight > 0
            && _activeScroll.Content is UIElement contentRoot)
        {
            var top = SectionHost.TransformToVisual(contentRoot).TransformPoint(default).Y;
            pinned = _activeScroll.VerticalOffset > top + 1;
        }

        SetOverlayVisibility(SubmissionNavOverlay, pinned && ViewModel.IsSubmissionTab, SyncSubmissionSelection);
        SetOverlayVisibility(MomentNavOverlay, pinned && ViewModel.IsMomentsTab, SyncMomentFilterSelection);
    }

    private static void SetOverlayVisibility(Grid overlay, bool show, Action sync)
    {
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (overlay.Visibility != visibility)
        {
            overlay.Visibility = visibility;
            if (show)
            {
                sync();
            }
        }
    }

    private void QueueScrollHostUpdate()
    {
        // 分区模板完成布局后只绑定一次，滚动本身完全交给原生容器。
        LayoutUpdated -= OnSpaceLayoutUpdated;
        LayoutUpdated += OnSpaceLayoutUpdated;
    }

    private void OnSpaceLayoutUpdated(object? sender, object e)
    {
        var root = ViewModel.IsSearchMode ? (DependencyObject)SearchSection : this;
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

        if (_activeScroll is not null)
        {
            _activeScroll.SizeChanged -= OnSpaceScrollSizeChanged;
            _activeScroll.ViewChanged -= OnSpaceScrollViewChanged;
        }

        if (_activeScrollHost is not null)
        {
            _activeScrollHost.BudgetChanged -= OnScrollHostBudgetChanged;
        }

        DisconnectHeader();
        _activeScroll = scroll;
        _activeScroll.SizeChanged += OnSpaceScrollSizeChanged;
        _activeScroll.ViewChanged += OnSpaceScrollViewChanged;
        _activeScrollHost = host;
        host.BudgetChanged += OnScrollHostBudgetChanged;
        _activeHeader = FindChild<UserSpacePageHeader>(scroll);
        if (_activeHeader is not null)
        {
            _activeHeader.SizeChanged += OnHeaderSizeChanged;
        }

        UpdateBackdropSize();
        ConnectBackdrop();
        UpdateSubmissionOverlay();
        SyncVideoDisplay();
    }

    private void DisconnectHeader()
    {
        if (_activeHeader is not null)
        {
            _activeHeader.SizeChanged -= OnHeaderSizeChanged;
            _activeHeader = null;
        }
    }

    private void CancelAvatarTransition()
    {
        StopAvatarTransition();
        if (_navigationProfile is { } profile
            && AvatarTransitionSession.Current is { } request
            && AvatarTransitionPolicy.Matches(
                request, profile.Id, profile.Avatar?.Uri))
        {
            AvatarConnectedAnimation.Cancel();
        }
    }

    private void OnSpaceScrollSizeChanged(object sender, SizeChangedEventArgs e) => UpdateSubmissionOverlay();

    private void OnSpaceScrollViewChanged(ScrollView sender, object args) => UpdateSubmissionOverlay();

    private void OnHeaderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBackdropSize();
        UpdateSubmissionOverlay();
    }

    private void UpdateBackdropSize()
    {
        // 背景延伸到首排内容之后，渐变不在页头或导航边缘终止。
        Backdrop.Height = (_activeHeader?.ActualHeight ?? 420) + 240;
    }

    private void ConnectBackdrop()
    {
        var surface = ElementCompositionPreview.GetElementVisual(Backdrop);
        var image = ElementCompositionPreview.GetElementVisual(BackdropImage);
        var fade = ElementCompositionPreview.GetElementVisual(BackdropFade);
        ElementCompositionPreview.SetIsTranslationEnabled(BackdropImage, true);
        ElementCompositionPreview.SetIsTranslationEnabled(BackdropFade, true);
        image.StopAnimation("Translation.Y");
        fade.StopAnimation("Translation.Y");
        image.Properties.InsertVector3("Translation", Vector3.Zero);
        fade.Properties.InsertVector3("Translation", Vector3.Zero);
        surface.Clip?.Dispose();
        surface.Clip = null;
        if (_activeScroll is null)
        {
            return;
        }

        using var parallax = surface.Compositor.CreateExpressionAnimation("-Max(0, source.Position.Y - source.MinPosition.Y) * speed");
        parallax.SetReferenceParameter("source", _activeScroll.ExpressionAnimationSources);
        parallax.SetScalarParameter("speed", _uiSettings.AnimationsEnabled ? 0.45f : 1f);
        image.StartAnimation("Translation.Y", parallax);
        parallax.SetScalarParameter("speed", 1f);
        fade.StartAnimation("Translation.Y", parallax);

        // 底色遮罩随内容上移，裁掉遮罩下方的图片，避免视差露出封面底边。
        var clip = surface.Compositor.CreateInsetClip();
        using var crop = surface.Compositor.CreateExpressionAnimation("Min(surface.Size.Y, Max(0, source.Position.Y - source.MinPosition.Y))");
        crop.SetReferenceParameter("source", _activeScroll.ExpressionAnimationSources);
        crop.SetReferenceParameter("surface", surface);
        clip.StartAnimation("BottomInset", crop);
        surface.Clip = clip;
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Backdrop.Width = e.NewSize.Width;
        PageLayout.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        UpdateSubmissionOverlay();
    }

    private void OnPageThemeChanged(FrameworkElement sender, object args) => UpdateBackdropColor();

    private void UpdateBackdropColor()
    {
        // 透明端保留底色的 RGB，避免 Transparent 的白色分量产生灰雾。
        var color = ((SolidColorBrush)PageLayout.Background).Color;
        color.A = 0;
        BackdropFadeStart.Color = color;
        color.A = 204;
        BackdropFadeMiddle.Color = color;
    }

    private static T? FindChild<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            return match;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindChild<T>(VisualTreeHelper.GetChild(root, i)) is { } child)
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
            // 复用页面时停在上一目标的滚动位置会让新目标从中段开始，先回到顶部。
            _activeScrollHost?.ResetScrollPosition();

            // 叠层按类型缓存页面，同一页面会再次收到新的用户参数。
            ViewModel.InitializeCommand.Execute(profile);

            // 记录本次页面发起的加载代次，卸载时只取消这一代，不影响新页面。
            _loadGeneration = ViewModel.CurrentLoadGeneration;
            AttachMomentSection();
            ViewModel.InitializeUserInformationCommand.Execute(default);

            // 叠层页会按类型缓存，复用页面不会再触发 Loaded；每次收到参数都重新等待布局。
            BeginAvatarTransition(profile);
            QueueSubmissionSelectionSync();
            SyncMomentFilterSelection();
            _skeletonFillRounds = 0;
            _videoSkeletonGaveUp = false;
        }
    }

    private void BeginAvatarTransition(UserProfile profile)
    {
        _navigationProfile = profile;
        LayoutUpdated -= OnAvatarLayoutUpdated;
        _avatarStartInProgress = false;

        if (profile.Avatar?.Uri is not { } avatar)
        {
            return;
        }

        var request = AvatarTransitionSession.Current;
        var canStart = request is not null
            && AvatarTransitionPolicy.CanStart(request, profile.Id, avatar, DateTimeOffset.Now);
        if (!canStart)
        {
            return;
        }

        // 页头头像按自己的尺寸重新解码，慢网络下会空一段时间；
        // 先把源头像已经解码好的位图顶上，串联落地时不会是空位.
        if ((_activeHeader ?? FindChild<UserSpacePageHeader>(this)) is { } header)
        {
            AvatarConnectedAnimation.TryPrime(header.AvatarElement);
        }

        // 新页面和复用页面都要等自身可见、页头完成布局后再启动，
        // 而不是依赖只触发一次的 Loaded。
        _avatarStartAttempts = 0;
        LayoutUpdated += OnAvatarLayoutUpdated;
    }

    private void OnAvatarLayoutUpdated(object? sender, object e)
    {
        if (_avatarStartInProgress)
        {
            return;
        }

        if (++_avatarStartAttempts > MaxAvatarStartAttempts)
        {
            // 页面始终没有完成布局：释放本页尚未启动的请求。
            CancelAvatarTransition();
            return;
        }

        if (_navigationProfile is not { } profile || profile.Avatar?.Uri is not { } avatar)
        {
            StopAvatarTransition();
            return;
        }

        var request = AvatarTransitionSession.Current;
        if (request is null)
        {
            StopAvatarTransition();
            return;
        }

        if (!AvatarTransitionPolicy.CanStart(request, profile.Id, avatar, DateTimeOffset.Now))
        {
            if (AvatarTransitionPolicy.Matches(request, profile.Id, avatar))
            {
                // 本页的请求已过期：释放悬停的源头像快照。
                AvatarConnectedAnimation.Cancel();
            }

            StopAvatarTransition();
            return;
        }

        if (Visibility != Visibility.Visible || ActualWidth <= 0)
        {
            return;
        }

        var header = _activeHeader ?? FindChild<UserSpacePageHeader>(this);
        if (header is null || header.AvatarElement.ActualWidth <= 0)
        {
            return;
        }

        _avatarStartInProgress = true;
        LayoutUpdated -= OnAvatarLayoutUpdated;
        StartAvatarTransition(header, profile.Id, avatar);
    }

    private void StartAvatarTransition(UserSpacePageHeader header, string userId, Uri avatar)
    {
        try
        {
            AvatarConnectedAnimation.TryStart(header.AvatarElement, userId, avatar);
        }
        finally
        {
            _avatarStartInProgress = false;
        }
    }

    private void StopAvatarTransition()
    {
        LayoutUpdated -= OnAvatarLayoutUpdated;
        _avatarStartInProgress = false;
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
