// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Moment;
using BiliCopilot.UI.Toolkits;
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
public sealed partial class UserSpacePage : UserSpacePageBase, IParameterPage, ICancelPageViewModel
{
    private const int MaxAvatarStartAttempts = 60;

    private readonly UISettings _uiSettings = new();
    private int _loadGeneration = -1;
    private ScrollView? _activeScroll;
    private IncrementVerticalScrollControl? _activeScrollHost;
    private UserSpacePageHeader? _activeHeader;
    private UserProfile? _navigationProfile;
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
        SubmissionSelector.SelectionChanged += OnSubmissionTabSelectionChanged;
        UpdateSubmissionTabs();
        QueueScrollHostUpdate();
    }

    private void OnSpaceUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        SubmissionSelector.SelectionChanged -= OnSubmissionTabSelectionChanged;
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
        else if (e.PropertyName is nameof(UserSpacePageViewModel.SubmissionTabs) or nameof(UserSpacePageViewModel.SelectedSubmissionTab))
        {
            UpdateSubmissionTabs();
        }
    }

    private void UpdateSubmissionTabs()
    {
        var items = ViewModel.SubmissionTabs
            .Select(p => new SelectorBarItem { Text = p.Title, Tag = p })
            .ToList();
        SubmissionSelector.SetItems(items, ViewModel.SelectedSubmissionTab);
    }

    private void OnSubmissionTabSelectionChanged(object? sender, EventArgs e)
    {
        if (SubmissionSelector.SelectedItem is UserSpaceTabViewModel tab && !ReferenceEquals(tab, ViewModel.SelectedSubmissionTab))
        {
            ViewModel.SelectSubmissionTabCommand.Execute(tab);
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

        DisconnectHeader();
        _activeScroll = scroll;
        _activeScrollHost = host;
        _activeHeader = FindChild<UserSpacePageHeader>(scroll);
        if (_activeHeader is not null)
        {
            _activeHeader.SizeChanged += OnHeaderSizeChanged;
        }

        UpdateBackdropSize();
        ConnectBackdrop();
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

    private void OnHeaderSizeChanged(object sender, SizeChangedEventArgs e) => UpdateBackdropSize();

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
            ViewModel.InitializeUserInformationCommand.Execute(default);

            // 叠层页会按类型缓存，复用页面不会再触发 Loaded；每次收到参数都重新等待布局。
            BeginAvatarTransition(profile);
            UpdateSubmissionTabs();
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
