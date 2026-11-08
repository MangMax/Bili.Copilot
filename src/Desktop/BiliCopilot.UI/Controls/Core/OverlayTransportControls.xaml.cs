// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Richasy.MpvKernel.Core.Enums;
using Richasy.WinUIKernel.Share.Toolkits;
using System.Numerics;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Controls.Core;

public sealed partial class OverlayTransportControls : PlayerControlBase
{
    public static readonly DependencyProperty LeftElementProperty =
        DependencyProperty.Register(nameof(LeftElement), typeof(object), typeof(OverlayTransportControls), new PropertyMetadata(default));

    public static readonly DependencyProperty ProgressBarVisibleProperty =
        DependencyProperty.Register(nameof(ProgressBarVisible), typeof(bool), typeof(OverlayTransportControls), new PropertyMetadata(true));

    public static readonly DependencyProperty IsSkipButtonsVisibleProperty =
        DependencyProperty.Register(nameof(IsSkipButtonsVisible), typeof(bool), typeof(OverlayTransportControls), new PropertyMetadata(true));

    private readonly List<double> _speedChangeList = [];
    private Func<bool>? _speedPlaybackValidity;
    private readonly List<double> _progressChangeList = [];
    private readonly DispatcherTimer _speedChangeTimer;
    private readonly DispatcherTimer _progressChangeTimer;
    private const int MotionDurationMs = 250;
    private readonly Dictionary<FlyoutBase, (FlyoutPlacementMode Placement, bool ConstrainToRootBounds)> _flyoutDefaults = [];
    private readonly UISettings _uiSettings = new();
    private bool _isFirstChangeVolume = true;
    private bool _isOverlayActive;
    private bool _isShown;
    private bool _motionActive;
    private int _motionVersion;
    private string? _appliedStateName;
    private OverlayControlLayoutMode _layoutMode;

    public OverlayTransportControls()
    {
        InitializeComponent();
        _speedChangeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
        _progressChangeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
        // The base removes its lifecycle handlers after the first unload.
        Loaded += OnOverlayLoaded;
        Unloaded += OnOverlayUnloaded;
    }

    public object LeftElement
    {
        get => (object)GetValue(LeftElementProperty);
        set => SetValue(LeftElementProperty, value);
    }

    public bool ProgressBarVisible
    {
        get => (bool)GetValue(ProgressBarVisibleProperty);
        set => SetValue(ProgressBarVisibleProperty, value);
    }

    public bool IsSkipButtonsVisible
    {
        get => (bool)GetValue(IsSkipButtonsVisibleProperty);
        set => SetValue(IsSkipButtonsVisibleProperty, value);
    }

    protected override void OnControlLoaded()
    {
        if (_isOverlayActive)
        {
            return;
        }

        _isOverlayActive = true;
        _uiSettings.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
        _speedChangeTimer.Tick += OnSpeedChangeTick;
        _progressChangeTimer.Tick += OnProgressChangeTick;
        if (ViewModel is not null)
        {
            ViewModel.ProgressChanged += OnViewModelProgressChanged;
            if (SubtitleRepeater is not null)
            {
                SubtitleRepeater.ItemsSource = ViewModel.Subtitles;
            }
        }

        UpdateLayoutState();
        ApplyFinal(_isShown);
    }

    private void OnOverlayLoaded(object sender, RoutedEventArgs e) => OnControlLoaded();

    private void OnOverlayUnloaded(object sender, RoutedEventArgs e) => OnControlUnloaded();

    protected override void OnViewModelChanged(PlayerViewModel? oldValue, PlayerViewModel? newValue)
    {
        _speedChangeTimer.Stop();
        _speedChangeList.Clear();
        _speedPlaybackValidity = null;
        if (oldValue != null)
        {
            oldValue.ProgressChanged -= OnViewModelProgressChanged;
        }

        if (_isOverlayActive && newValue != null)
        {
            newValue.ProgressChanged += OnViewModelProgressChanged;
        }
    }

    protected override void OnControlUnloaded()
    {
        if (!_isOverlayActive)
        {
            return;
        }

        _isOverlayActive = false;
        _uiSettings.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        _motionVersion++;
        _motionActive = false;
        _speedChangeList.Clear();
        _progressChangeList.Clear();
        _speedPlaybackValidity = null;
        _speedChangeTimer?.Stop();
        _progressChangeTimer?.Stop();

        if (_speedChangeTimer != null)
        {
            _speedChangeTimer.Tick -= OnSpeedChangeTick;
        }

        if (_progressChangeTimer != null)
        {
            _progressChangeTimer.Tick -= OnProgressChangeTick;
        }

        if (ViewModel is not null)
        {
            ViewModel.ProgressChanged -= OnViewModelProgressChanged;
        }
        SubtitleRepeater?.ItemsSource = null;
    }

#pragma warning disable CA1822
    private FluentIcons.Common.Symbol GetPlayPauseSymbol(MpvPlayerState state)
        => state == MpvPlayerState.Playing ? FluentIcons.Common.Symbol.Pause : FluentIcons.Common.Symbol.Play;

    private FluentIcons.Common.Symbol GetCompactOverlaySymbol(bool isCompactOverlay)
        => isCompactOverlay ? FluentIcons.Common.Symbol.ContractDownLeft : FluentIcons.Common.Symbol.ContractUpRight;

    private FluentIcons.Common.Symbol GetFullScreenSymbol(bool isFullScreen)
        => isFullScreen ? FluentIcons.Common.Symbol.FullScreenMinimize : FluentIcons.Common.Symbol.FullScreenMaximize;

    private bool IsPlayPauseEnabled(MpvPlayerState state)
        => state is MpvPlayerState.Playing or MpvPlayerState.Paused or MpvPlayerState.End;

    private string GetPlayPauseTip(bool isPlaying)
        => isPlaying ? ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Pause) : ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Play);

    private bool IsLoading(MpvPlayerState state, bool isLoading)
        => state is MpvPlayerState.Seeking or MpvPlayerState.Buffering || isLoading;

    private string GetTimeText(double seconds)
        => TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");

    private string GetSpeedText(double speed)
        => $"{ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.PlaybackRate)}: {Math.Round(speed, 2)}x";

    private string GetCompactOverlayTip(bool isCompactOverlay)
        => isCompactOverlay ? ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.ExitCompactOverlay) : ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.EnterCompactOverlay);

    private string GetFullScreenTip(bool isFullScreen)
        => isFullScreen ? ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.ExitFullScreen) : ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.EnterFullScreen);

    // 进度由自绘的 ChapterSegmentBar 通过 Player.Position 直接驱动，
    // 此处不再需要同步 Slider，保留空实现以免改动订阅关系.
    private void OnViewModelProgressChanged(object? sender, double e)
    {
    }

    private async void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.PlayPauseCommand.ExecuteAsync(default);
    }

    private async void OnVolumeValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var newValue = e.NewValue;
        var isCurrent = ViewModel.CapturePlaybackValidity();
        if (!isCurrent() || ViewModel.Player?.IsPlaybackInitialized != true)
        {
            return;
        }

        if (_isFirstChangeVolume && (ViewModel.Player.Volume > 100 && newValue == 100))
        {
            await Task.Delay(400);
            // 延迟期间可能已经关闭或换集，写回旧会话没有意义。
            if (!isCurrent())
            {
                return;
            }

            ViewModel.Player.RaisePropertyChanged(nameof(ViewModel.Player.Volume));
            _isFirstChangeVolume = false;
            return;
        }

        if (Math.Abs(newValue - ViewModel.Player.Volume) < 1)
        {
            return;
        }

        await ViewModel.ChangeVolumeAsync(newValue);
    }

    private void OnSpeedValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isOverlayActive)
        {
            return;
        }

        var newValue = e.NewValue;
        CheckSpeedButtonState();
        if (ViewModel?.Client is null || ViewModel.Player?.IsPlaybackInitialized != true)
        {
            return;
        }

        if (Math.Abs(newValue - ViewModel.Player.PlaybackRate) < 0.03)
        {
            return;
        }

        _speedChangeList.Add(newValue);
        _speedPlaybackValidity = ViewModel.CapturePlaybackValidity();
        _speedChangeTimer.Stop();
        _speedChangeTimer.Start();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateLayoutState();

    /// <summary>
    /// 指针是否落在会唤出 / 保留控件的区域（底部带，或竖屏轨道）。
    /// 轨道铺满播放区高度，因此不能再用固定高度判断，否则鼠标移到轨道上会把控件判成已离开.
    /// </summary>
    internal bool ContainsInteractivePoint(Point point)
    {
        var (width, height) = ResolveAvailableSize();
        // Hidden controls do not receive layout events. Use the current window
        // shape here so the right rail can wake them after a hidden transition.
        var mode = OverlayControlLayout.SelectMode(IsCompactOverlayWindow(), width, height);
        return OverlayControlLayout.IsInsideInteractiveArea(mode, width, height, point.X, point.Y);
    }

    // Keep the layout measured while hidden; animate only the visible command
    // surface(s). Completion comes from Composition, not an estimated delay.
    internal void SetShown(bool isShown, bool animate = true)
    {
        if (_isShown == isShown && animate)
        {
            return;
        }

        _isShown = isShown;
        UpdateLayoutState();
        if (!IsLoaded || !animate || !_uiSettings.AnimationsEnabled)
        {
            ApplyFinal(isShown);
            return;
        }

        var continuing = _motionActive;
        var version = ++_motionVersion;
        _motionActive = true;
        IsEnabled = isShown;
        IsHitTestVisible = isShown;
        Opacity = 1d;
        var compositor = ElementCompositionPreview.GetElementVisual(RootGrid).Compositor;
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        if (_layoutMode == OverlayControlLayoutMode.PortraitRail)
        {
            AnimateVisual(ControlsScroller, isShown, new Vector3((float)OverlayControlLayout.RailWidth, 0, 0), continuing);
            AnimateVisual(ProgressHost, isShown, new Vector3(0, (float)OverlayControlLayout.BottomEnterOffset, 0), continuing);
        }
        else
        {
            AnimateVisual(RootGrid, isShown, new Vector3(0, (float)OverlayControlLayout.BottomEnterOffset, 0), continuing);
        }

        batch.Completed += (_, _) =>
        {
            if (version == _motionVersion && _isOverlayActive)
            {
                ApplyFinal(_isShown);
            }

            batch.Dispose();
        };
        batch.End();
    }

    private void ApplyFinal(bool visible)
    {
        SettleMotion();
        IsEnabled = visible;
        IsHitTestVisible = visible;
        Opacity = visible ? 1d : 0d;
    }

    private void SettleMotion()
    {
        ++_motionVersion;
        _motionActive = false;
        if (!IsLoaded)
        {
            return;
        }

        foreach (var element in new FrameworkElement[] { RootGrid, ControlsScroller, ProgressHost })
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.StopAnimation("Translation");
            visual.StopAnimation("Opacity");
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
            visual.Opacity = 1f;
        }
    }

    private static void AnimateVisual(FrameworkElement element, bool showing, Vector3 offset, bool continuing)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f));
        var translation = compositor.CreateVector3KeyFrameAnimation();
        translation.Duration = TimeSpan.FromMilliseconds(MotionDurationMs);
        if (continuing)
        {
            translation.InsertExpressionKeyFrame(0f, "this.StartingValue");
        }
        else
        {
            translation.InsertKeyFrame(0f, showing ? offset : Vector3.Zero);
        }

        translation.InsertKeyFrame(1f, showing ? Vector3.Zero : offset, easing);
        visual.StartAnimation("Translation", translation);
        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = translation.Duration;
        if (continuing)
        {
            opacity.InsertExpressionKeyFrame(0f, "this.StartingValue");
        }
        else
        {
            opacity.InsertKeyFrame(0f, showing ? 0f : 1f);
        }

        opacity.InsertKeyFrame(1f, showing ? 1f : 0f);
        visual.StartAnimation("Opacity", opacity);
    }

    private void OnAnimationsEnabledChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args)
        => DispatcherQueue.TryEnqueue(() =>
        {
            if (_isOverlayActive && !_uiSettings.AnimationsEnabled)
            {
                ApplyFinal(_isShown);
            }
        });

    private void OnControlsWheel(object sender, PointerRoutedEventArgs e)
    {
        if (_layoutMode == OverlayControlLayoutMode.PortraitRail)
        {
            // Keep wheel input within the scrollable command rail, including its edges.
            e.Handled = true;
        }
    }

    /// <summary>
    /// 只依据真实窗口模式与实际可用区域决定排布：竖屏小窗用右侧轨道，其余保持底部带。
    /// 媒体切换、手动缩放、进入/退出小窗都会改变尺寸，从而重新走这里恢复状态.
    /// </summary>
    private void UpdateLayoutState()
    {
        var (width, height) = ResolveAvailableSize();
        var mode = OverlayControlLayout.SelectMode(IsCompactOverlayWindow(), width, height);
        var changed = mode != _layoutMode;
        _layoutMode = mode;

        var bandState = OverlayControlLayout.SelectBandState(width, ViewModel?.IsDanmakuControlVisible == true);
        ApplyRootGeometry(mode, bandState == OverlayBandState.Narrow);

        var stateName = mode == OverlayControlLayoutMode.PortraitRail ? nameof(PortraitState) : ToStateName(bandState);
        if (stateName != _appliedStateName)
        {
            VisualStateManager.GoToState(this, stateName, false);
            _appliedStateName = stateName;
        }

        ApplyFlyoutConstraints(mode);

        if (changed)
        {
            ConfigureRailFlyouts(mode == OverlayControlLayoutMode.PortraitRail);
            // 横竖切换时先停掉旧方向的动画并落到最终位置，避免右栏带着横向位移
            // 或底部带带着纵向位移跨越新布局；隐藏状态则直接收起，等待下次唤出重新判定.
            SettleMotion();
            if (!_isShown)
            {
                IsHitTestVisible = false;
                Opacity = 0d;
            }
        }

        if (mode == OverlayControlLayoutMode.Band)
        {
            BalanceLeftRightWidth();
        }
    }

    private (double Width, double Height) ResolveAvailableSize()
    {
        // 可用区域取 XAML 岛（即播放窗口客户区）的尺寸：控件被折叠时它仍然有效，
        // 而控件自身在隐藏后会被测量成 0.
        var size = XamlRoot?.Size;
        if (size is { Width: > 0, Height: > 0 })
        {
            return (size.Value.Width, size.Value.Height);
        }

        return (ActualWidth, ActualHeight);
    }

    private bool IsCompactOverlayWindow()
    {
        var window = ViewModel?.Window;
        if (window is null || window.IsClosed)
        {
            return false;
        }

        return window.GetWindow().Presenter.Kind == AppWindowPresenterKind.CompactOverlay;
    }

    private static string ToStateName(OverlayBandState state)
        => state switch
        {
            OverlayBandState.Narrow => nameof(NarrowState),
            OverlayBandState.Medium => nameof(MediumState),
            OverlayBandState.BelowWide => nameof(BelowWideState),
            _ => nameof(WideState),
        };

    /// <summary>
    /// 底部带保持固定高度（窄宽度 120），竖屏轨道则撑满播放区，由 ScrollViewer 负责滚动。
    /// 高度只有一个拥有者：这里。
    /// </summary>
    private void ApplyRootGeometry(OverlayControlLayoutMode mode, bool isNarrow)
    {
        if (mode == OverlayControlLayoutMode.PortraitRail)
        {
            RootGrid.Height = double.NaN;
            RootGrid.VerticalAlignment = VerticalAlignment.Stretch;
            return;
        }

        RootGrid.Height = isNarrow ? 120d : 160d;
        RootGrid.VerticalAlignment = VerticalAlignment.Bottom;
    }

    /// <summary>
    /// 竖屏轨道里弹出面板向左展开，并且不越出小窗；
    /// 退出轨道时恢复各面板原本的展开方向与约束设置.
    /// </summary>
    private void ConfigureRailFlyouts(bool isRail)
    {
        ConfigureRailFlyout(ChapterListButton?.Flyout, isRail);
        ConfigureRailFlyout(SlimVolumeButton?.Flyout, isRail);
        ConfigureRailFlyout(SpeedButton?.Flyout, isRail);
        ConfigureRailFlyout(SubtitleButton?.Flyout, isRail);
        ConfigureRailFlyout(OverflowButton?.Flyout, isRail);

        ConfigureRailFlyout(DanmakuOptionButton?.Flyout, isRail);

    }

    private void ConfigureRailFlyout(FlyoutBase? flyout, bool isRail)
    {
        if (flyout is null)
        {
            return;
        }

        if (!_flyoutDefaults.TryGetValue(flyout, out var defaults))
        {
            defaults = (flyout.Placement, flyout is Flyout flyoutPresenter && flyoutPresenter.ShouldConstrainToRootBounds);
            _flyoutDefaults[flyout] = defaults;
        }

        flyout.Placement = isRail ? FlyoutPlacementMode.Left : defaults.Placement;
        if (flyout is Flyout target)
        {
            target.ShouldConstrainToRootBounds = isRail || defaults.ConstrainToRootBounds;
        }
    }

    /// <summary>
    /// 把各弹层内容收进实际可用宽高：设计宽度只在窗口放得下时使用，
    /// 竖屏轨道还要给右侧轨道让位。尺寸变化会重新计算，退出轨道后自然恢复设计尺寸；
    /// 高度同时约束滚动区，避免内容被 FlyoutPresenter 裁掉而不是滚动.
    /// </summary>
    private void ApplyFlyoutConstraints(OverlayControlLayoutMode mode)
    {
        var (width, height) = ResolveAvailableSize();
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return;
        }

        var isRail = mode == OverlayControlLayoutMode.PortraitRail;
        ConfigurePortraitFlyoutContent(isRail);

        ChapterFlyoutPanel.Width = OverlayFlyoutLayout.ContentWidth(
            OverlayFlyoutLayout.ChapterPreferredWidth, width, isRail);
        ChapterListScroller.MaxHeight = OverlayFlyoutLayout.ScrollHeight(
            OverlayFlyoutLayout.ChapterListPreferredHeight, height, OverlayFlyoutLayout.ChapterHeaderHeight);

        // 音量：竖屏使用专为窄轨设计的宽度 + 纵向滑块；底部带保持原来的 200 宽横滑块.
        VolumeFlyoutScroller.Width = OverlayFlyoutLayout.ContentWidth(
            isRail ? OverlayFlyoutLayout.PortraitVolumePreferredWidth : OverlayFlyoutLayout.VolumePreferredWidth,
            width,
            isRail);
        VolumeFlyoutScroller.MaxHeight = OverlayFlyoutLayout.ContentHeight(
            isRail ? OverlayFlyoutLayout.PortraitVolumePreferredHeight : 520d,
            height);

        // 倍速：竖屏把滑块竖起、快捷项排成一列；底部带仍是 350 宽横排.
        SpeedFlyoutScroller.Width = OverlayFlyoutLayout.ContentWidth(
            isRail ? OverlayFlyoutLayout.PortraitSpeedPreferredWidth : OverlayFlyoutLayout.SpeedPreferredWidth,
            width,
            isRail);
        SpeedFlyoutScroller.MaxHeight = OverlayFlyoutLayout.ContentHeight(
            isRail ? OverlayFlyoutLayout.PortraitSpeedPreferredHeight : 520d,
            height);

        // 字幕弹层把 FlyoutPresenter 的 Padding 覆盖成了 0，因此这里不再扣 presenter 内边距.
        SubtitleFlyoutScroller.Width = OverlayFlyoutLayout.ContentWidth(
            OverlayFlyoutLayout.SubtitlePreferredWidth, width, isRail, presenterHorizontalPadding: 0d);
        SubtitleFlyoutScroller.MaxHeight = OverlayFlyoutLayout.ContentHeight(
            OverlayFlyoutLayout.SubtitlePreferredHeight, height);

        DanmakuOptionsScroller.Width = OverlayFlyoutLayout.ContentWidth(
            OverlayFlyoutLayout.DanmakuPreferredWidth, width, isRail);
        DanmakuOptionsScroller.MaxHeight = OverlayFlyoutLayout.ContentHeight(
            OverlayFlyoutLayout.DanmakuPreferredHeight, height);
    }

    /// <summary>
    /// 竖屏的弹层不是把原横向面板缩窄：音量/倍速滑块改为纵向，
    /// 倍速快捷项从六列横排重排成一列纵排；退出轨道时全部恢复原横向设置.
    /// </summary>
    private void ConfigurePortraitFlyoutContent(bool isRail)
    {
        VolumeSlider.Orientation = isRail ? Orientation.Vertical : Orientation.Horizontal;
        VolumeSlider.Height = isRail ? OverlayFlyoutLayout.PortraitSliderHeight : double.NaN;
        VolumeSlider.Width = isRail ? OverlayFlyoutLayout.PortraitSliderWidth : double.NaN;
        VolumeSlider.HorizontalAlignment = isRail ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;

        SpeedSlider.Orientation = isRail ? Orientation.Vertical : Orientation.Horizontal;
        SpeedSlider.Height = isRail ? OverlayFlyoutLayout.PortraitSliderHeight : double.NaN;
        SpeedSlider.Width = isRail ? OverlayFlyoutLayout.PortraitSliderWidth : double.NaN;
        SpeedSlider.HorizontalAlignment = isRail ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;

        var columns = OverlayFlyoutLayout.SpeedQuickColumns(isRail);
        var buttons = new[]
        {
            HalfSpeedButton,
            DefaultSpeedButton,
            OneTwoFiveSpeedButton,
            OneHalfSpeedButton,
            DoubleSpeedButton,
            TripleSpeedButton,
        };
        for (var i = 0; i < buttons.Length; i++)
        {
            Grid.SetRow(buttons[i], columns == 1 ? i : 0);
            Grid.SetColumn(buttons[i], columns == 1 ? 0 : i);
        }

        for (var column = 0; column < SpeedQuickGrid.ColumnDefinitions.Count; column++)
        {
            SpeedQuickGrid.ColumnDefinitions[column].Width = column < columns
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0d);
        }
    }

    private void BalanceLeftRightWidth()
    {
        var width = Math.Min(LeftHolderGrid.ActualWidth, RightPanel.ActualWidth);
        if (width == 0 && RightPanel.ActualWidth > 0)
        {
            width = RightPanel.ActualWidth;
        }

        if (width < 300 && LeftHolderGrid.ActualWidth > 300)
        {
            width = 300;
        }

        LeftPresenter.Width = width;
    }

    private async void OnSpeedChangeTick(object? sender, object e)
    {
        _speedChangeTimer.Stop();
        var isCurrent = _speedPlaybackValidity;
        _speedPlaybackValidity = null;
        var lastSpeed = _speedChangeList.LastOrDefault();
        _speedChangeList.Clear();
        if (_isOverlayActive && isCurrent?.Invoke() == true && Math.Abs(lastSpeed - ViewModel.Player.PlaybackRate) > 0.1)
        {
            if (await ViewModel.ChangeSpeedAsync(lastSpeed) && isCurrent?.Invoke() == true)
            {
                CheckSpeedButtonState();
            }
        }
    }

    private async void OnProgressChangeTick(object? sender, object e)
    {
        var isCurrent = ViewModel.CapturePlaybackValidity();
        var lastProgress = _progressChangeList.LastOrDefault();
        _progressChangeList.Clear();
        if (!isCurrent())
        {
            _progressChangeTimer.Stop();
            return;
        }

        if (lastProgress != ViewModel.Player.Position)
        {
            if (ViewModel.IsNextTipShown)
            {
                ViewModel.HideNextTipCommand.Execute(default);
            }

            await ViewModel.ChangePositionAsync(lastProgress);
        }

        _progressChangeList.Clear();
        _progressChangeTimer.Stop();
    }

    private void OnSubtitleFlyoutOpened(object sender, object e)
    {
        ViewModel.IsPopupVisible = true;
    }

    private void OnSubtitleFlyoutClosed(object sender, object e)
    {
        ViewModel.IsPopupVisible = false;
        // 菜单关闭后复位层级，下次打开从第一层开始。
        ViewModel.IsSubtitleParameterVisible = false;
    }

    private void OnRightPanelSizeChanged(object sender, SizeChangedEventArgs e)
        => BalanceLeftRightWidth();

    private async void CheckSpeedButtonState()
    {
        if (ViewModel is null)
        {
            return;
        }

        var isCurrent = ViewModel.CapturePlaybackValidity();
        await Task.Delay(300);
        if (!isCurrent())
        {
            return;
        }

        var speed = ViewModel.Player?.PlaybackRate ?? 0;
        HalfSpeedButton.IsChecked = false;
        DefaultSpeedButton.IsChecked = false;
        OneTwoFiveSpeedButton.IsChecked = false;
        OneHalfSpeedButton.IsChecked = false;
        DoubleSpeedButton.IsChecked = false;
        TripleSpeedButton.IsChecked = false;
        if (Math.Abs(speed - 0.5) < 0.01)
        {
            HalfSpeedButton.IsChecked = true;
        }
        else if (Math.Abs(speed - 1) < 0.01)
        {
            DefaultSpeedButton.IsChecked = true;
        }
        else if (Math.Abs(speed - 1.25) < 0.01)
        {
            OneTwoFiveSpeedButton.IsChecked = true;
        }
        else if (Math.Abs(speed - 1.5) < 0.01)
        {
            OneHalfSpeedButton.IsChecked = true;
        }
        else if (Math.Abs(speed - 2) < 0.01)
        {
            DoubleSpeedButton.IsChecked = true;
        }
        else if (Math.Abs(speed - 3) < 0.01)
        {
            TripleSpeedButton.IsChecked = true;
        }
    }

    private async void OnQuickSpeedButtonClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsSessionCurrent())
        {
            return;
        }

        var speed = Convert.ToDouble((sender as FrameworkElement)?.Tag);
        _speedChangeList.Clear();
        _speedPlaybackValidity = null;
        _speedChangeTimer.Stop();
        if (Math.Abs(speed - ViewModel.Player.PlaybackRate) > 0.1)
        {
            await ViewModel.ChangeSpeedAsync(speed);
        }
        else
        {
            CheckSpeedButtonState();
        }
    }

    private void OnFlyoutClosed(object sender, object e)
        => ViewModel.IsPopupVisible = false;

    private async void OnSubtitleParameterEntryClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsSubtitleParameterVisible = true;
        // 入口按钮随即被折叠，把焦点交给第二层的返回按钮，键盘用户不会失去落点。
        MoveSubtitleParameterFocus(SubtitleParameterBackButton);
        // 每次进入第二层都重新确认字体列表并回显当前字体，首次加载失败时下次进入会重试。
        await EnsureSubtitleFontSelectionAsync();
    }

    private void OnSubtitleParameterBackClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsSubtitleParameterVisible = false;
        // 返回第一层后把焦点还给参数页入口。
        MoveSubtitleParameterFocus(SubtitleParameterEntryButton);
    }

    private void MoveSubtitleParameterFocus(Microsoft.UI.Xaml.Controls.Control target)
        => DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => target.Focus(FocusState.Programmatic));

    private void OnSubtitlePositionChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // 参数控件在 XAML 解析阶段就会触发一次（Slider 钳制初始 Value），此时 ViewModel 还没由父级赋值。
        if (ViewModel?.Player?.IsPlaybackInitialized != true || ViewModel.SubtitlePosition == e.NewValue)
        {
            return;
        }

        ViewModel.ChangeSubtitlePositionCommand.Execute(e.NewValue);
    }

    private void OnSubtitleFontSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // 同上：Minimum=1 会在解析期把默认 Value 从 0 钳到 1，从而在 ViewModel 尚未赋值时触发本处理器。
        if (ViewModel?.Player?.IsPlaybackInitialized != true || ViewModel.SubtitleFontSize == e.NewValue)
        {
            return;
        }

        ViewModel.ChangeSubtitleFontSizeCommand.Execute(e.NewValue);
    }

    private void OnSubtitleFontChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = e.AddedItems.FirstOrDefault() as SystemFont;
        if (ViewModel?.Player?.IsPlaybackInitialized != true || item == null || ViewModel.SubtitleFontFamily == item.LocalName)
        {
            return;
        }

        ViewModel.ChangeSubtitleFontFamilyCommand.Execute(item.LocalName);
    }

    private void OnSubtitleDelayChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        var value = args.NewValue;
        if (ViewModel is null || ViewModel.SubtitleDelaySeconds == value)
        {
            return;
        }

        ViewModel.SetSubtitleDelaySecondsCommand.Execute(value);
    }

    private async void OnSubtitleFontComboLoaded(object sender, RoutedEventArgs e)
        => await EnsureSubtitleFontSelectionAsync();

    private async Task EnsureSubtitleFontSelectionAsync()
    {
        if (ViewModel is null || !SubtitleFontComboBox.IsLoaded)
        {
            return;
        }

        try
        {
            await ViewModel.ReloadFontsCommand.ExecuteAsync(default);
        }
        catch (Exception ex)
        {
            // 字体目录读取失败只影响回显；字体列表仍为空时，下次进入会再次尝试。
            this.Get<ILogger<OverlayTransportControls>>().LogWarning(ex, "Unable to load subtitle font choices");
        }

        if (!SubtitleFontComboBox.IsLoaded)
        {
            return;
        }

        var font = ViewModel.SubtitleFontFamily;
        var source = ViewModel.Fonts.FirstOrDefault(p => p.LocalName == font);
        if (source != null)
        {
            SubtitleFontComboBox.SelectedItem = source;
        }
    }

    private void OnDisplayFlyoutOpened(object sender, object e)
    {
        ViewModel.IsPopupVisible = true;
    }

    private void OnDanmakuOptionsClosed(object sender, object args)
    {
        ViewModel.IsPopupVisible = false;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (ViewModel.Window?.IsClosed != false)
            {
                return;
            }

            ViewModel.Danmaku.ResetStyle();
        });
    }
#pragma warning restore CA1822
}
