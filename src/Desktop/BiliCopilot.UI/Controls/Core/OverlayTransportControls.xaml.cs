// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Richasy.MpvKernel.Core.Enums;
using Richasy.WinUIKernel.Share.Toolkits;

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
    private readonly Dictionary<FlyoutBase, (FlyoutPlacementMode Placement, bool ConstrainToRootBounds)> _flyoutDefaults = [];
    private bool _isFirstChangeVolume = true;
    private bool _isOverlayActive;
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
        return OverlayControlLayout.IsInsideInteractiveArea(_layoutMode, width, height, point.X, point.Y);
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

        VisualStateManager.GoToState(
            this,
            mode == OverlayControlLayoutMode.PortraitRail ? nameof(PortraitState) : ToStateName(bandState),
            false);

        if (changed)
        {
            ConfigureRailFlyouts(mode == OverlayControlLayoutMode.PortraitRail);
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

        if (DanmakuOptionsTip is not null)
        {
            DanmakuOptionsTip.PreferredPlacement = isRail
                ? TeachingTipPlacementMode.Left
                : TeachingTipPlacementMode.TopLeft;
        }
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
    /// 轨道区域的滚轮只用于滚动控件；不可滚动时也不能冒泡去触发播放器的音量快捷键.
    /// </summary>
    private void OnControlsWheel(object sender, PointerRoutedEventArgs e)
    {
        if (_layoutMode == OverlayControlLayoutMode.PortraitRail)
        {
            e.Handled = true;
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

    private void OnDisplayFlyoutClosed(object sender, object e)
    {
        ViewModel.IsPopupVisible = false;
        ViewModel.Danmaku.ResetStyle();
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

    private void OnDanmakuOptionsButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsDanmakuOptionsVisible = !ViewModel.IsDanmakuOptionsVisible;
    }

    private void OnDanmakuOptionsClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
    {
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
