// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Richasy.WinUIKernel.Share.Base;
using System.ComponentModel;
using System.Numerics;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 番剧/电影/电视剧详情页.
/// </summary>
public sealed partial class PgcDetailPage : PgcDetailPageBase, IParameterPage, ICancelPageViewModel
{
    private const int MaxCoverStartAttempts = 60;

    private readonly UISettings _uiSettings = new();
    private string? _seasonId;
    private int _coverStartAttempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgcDetailPage"/> class.
    /// </summary>
    public PgcDetailPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloadedEvent;
        TabSelector.SelectionChanged += OnTabSelectionChanged;
    }

    /// <inheritdoc/>
    public void SetParameter(object? parameter)
    {
        if (parameter is string seasonId)
        {
            // 叠层页按类型缓存，复用时只换参数.
            ViewModel.InitializeCommand.Execute(seasonId);
            _seasonId = seasonId;
            BeginCoverTransition();
        }
    }

    /// <inheritdoc/>
    public void CancelLoading() => ViewModel.CancelLoad();

    /// <inheritdoc/>
    protected override void OnPageUnloaded() => CancelLoading();

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateTabs();
        ConnectBackdrop();
    }

    private void OnPageUnloadedEvent(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        LayoutUpdated -= OnCoverLayoutUpdated;
        if (_seasonId is { } seasonId
            && AvatarTransitionSession.Current is { } request
            && AvatarTransitionPolicy.MatchesTarget(request, seasonId))
        {
            AvatarConnectedAnimation.Cancel();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PgcDetailPageViewModel.Tabs) or nameof(PgcDetailPageViewModel.SelectedTab))
        {
            UpdateTabs();
        }
    }

    private void UpdateTabs()
    {
        var items = ViewModel.Tabs
            .Select(p => new Microsoft.UI.Xaml.Controls.SelectorBarItem { Text = p.Title, Tag = p })
            .ToList();
        TabSelector.SetItems(items, ViewModel.SelectedTab);
    }

    private void OnTabSelectionChanged(object? sender, EventArgs e)
    {
        if (TabSelector.SelectedItem is PgcDetailTabViewModel tab && !ReferenceEquals(tab, ViewModel.SelectedTab))
        {
            ViewModel.SelectTab(tab);
        }
    }

    private void OnEpisodeClick(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: EpisodeItemViewModel episode })
        {
            ViewModel.PlayEpisodeCommand.Execute(episode);
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Backdrop.Width = e.NewSize.Width;
        PageLayout.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
    }

    /// <summary>
    /// 背景随内容滚动做视差，和用户空间一致.
    /// </summary>
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
        if (PageScroll is null)
        {
            return;
        }

        using var parallax = surface.Compositor.CreateExpressionAnimation("-Max(0, source.Position.Y - source.MinPosition.Y) * speed");
        parallax.SetReferenceParameter("source", PageScroll.ExpressionAnimationSources);
        parallax.SetScalarParameter("speed", _uiSettings.AnimationsEnabled ? 0.45f : 1f);
        image.StartAnimation("Translation.Y", parallax);
        parallax.SetScalarParameter("speed", 0.7f);
        fade.StartAnimation("Translation.Y", parallax);

        // 裁掉视差露出的封面底边.
        var clip = surface.Compositor.CreateInsetClip();
        using var crop = surface.Compositor.CreateExpressionAnimation("Min(surface.Size.Y, Max(0, source.Position.Y - source.MinPosition.Y))");
        crop.SetReferenceParameter("source", PageScroll.ExpressionAnimationSources);
        crop.SetReferenceParameter("surface", surface);
        clip.StartAnimation("BottomInset", crop);
        surface.Clip = clip;
    }

    private void BeginCoverTransition()
    {
        LayoutUpdated -= OnCoverLayoutUpdated;
        _coverStartAttempts = 0;
        if (AvatarTransitionSession.Current is null)
        {
            return;
        }

        LayoutUpdated += OnCoverLayoutUpdated;
    }

    private void OnCoverLayoutUpdated(object? sender, object e)
    {
        if (++_coverStartAttempts > MaxCoverStartAttempts)
        {
            LayoutUpdated -= OnCoverLayoutUpdated;
            return;
        }

        if (_seasonId is not { Length: > 0 } seasonId
            || Visibility != Visibility.Visible
            || ActualWidth <= 0
            || PosterCover.ActualWidth <= 0)
        {
            return;
        }

        LayoutUpdated -= OnCoverLayoutUpdated;
        AvatarConnectedAnimation.TryStartForTarget(PosterCover, seasonId);
    }
}

/// <summary>
/// 番剧详情页基类.
/// </summary>
public abstract class PgcDetailPageBase : LayoutPageBase<PgcDetailPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcDetailPageBase"/> class.
    /// </summary>
    protected PgcDetailPageBase()
    {
        ViewModel = this.Get<PgcDetailPageViewModel>();
    }
}
