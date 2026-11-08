// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.Extensions.Logging;
using Richasy.MpvKernel.Core.Enums;
using Richasy.WinUIKernel.Share.Toolkits;
using System.ComponentModel;

namespace BiliCopilot.UI.Controls.Core;

public sealed partial class OverlayTransportControls : PlayerControlBase
{
    public static readonly DependencyProperty LeftElementProperty =
        DependencyProperty.Register(nameof(LeftElement), typeof(object), typeof(OverlayTransportControls), new PropertyMetadata(default));

    public static readonly DependencyProperty ProgressBarVisibleProperty =
        DependencyProperty.Register(nameof(ProgressBarVisible), typeof(bool), typeof(OverlayTransportControls), new PropertyMetadata(true));

    public static readonly DependencyProperty IsSkipButtonsVisibleProperty =
        DependencyProperty.Register(nameof(IsSkipButtonsVisible), typeof(bool), typeof(OverlayTransportControls), new PropertyMetadata(true));

    private const double PlaybackRateTolerance = 0.01;

    private double? _pendingSpeed;
    private Func<bool>? _speedPlaybackValidity;
    private readonly DispatcherTimer _speedChangeTimer;
    private bool _isFirstChangeVolume = true;
    private bool _isOverlayActive;
    private XamlRoot? _observedRoot;
    private (OverlayControlMode Mode, double Width, double Height, bool Danmaku)? _appliedLayout;

    public OverlayTransportControls()
    {
        InitializeComponent();
        _speedChangeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
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
        _appliedLayout = null;
        _speedChangeTimer.Tick += OnSpeedChangeTick;
        if (ViewModel is not null)
        {
            if (SubtitleRepeater is not null)
            {
                SubtitleRepeater.ItemsSource = ViewModel.Subtitles;
            }
        }

        _observedRoot = XamlRoot;
        if (_observedRoot is not null)
        {
            _observedRoot.Changed += OnRootChanged;
        }
        ViewModel.Player.PropertyChanged += OnPlayerPropertyChanged;
        UpdateLayoutState();
    }

    private void OnOverlayLoaded(object sender, RoutedEventArgs e) => OnControlLoaded();

    private void OnOverlayUnloaded(object sender, RoutedEventArgs e) => OnControlUnloaded();

    protected override void OnViewModelChanged(PlayerViewModel? oldValue, PlayerViewModel? newValue)
    {
        _appliedLayout = null;
        _speedChangeTimer.Stop();
        _pendingSpeed = null;
        _speedPlaybackValidity = null;
        if (oldValue != null)
        {
            oldValue.Player.PropertyChanged -= OnPlayerPropertyChanged;
        }

        if (_isOverlayActive && newValue != null)
        {
            newValue.Player.PropertyChanged += OnPlayerPropertyChanged;
            UpdateLayoutState();
        }
    }

    protected override void OnControlUnloaded()
    {
        if (!_isOverlayActive)
        {
            return;
        }

        if (_observedRoot is not null)
        {
            _observedRoot.Changed -= OnRootChanged;
            _observedRoot = null;
        }
        ViewModel.Player.PropertyChanged -= OnPlayerPropertyChanged;
        _isOverlayActive = false;
        _appliedLayout = null;
        _pendingSpeed = null;
        _speedPlaybackValidity = null;
        _speedChangeTimer.Stop();
        _speedChangeTimer.Tick -= OnSpeedChangeTick;

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

        if (Math.Abs(newValue - ViewModel.Player.PlaybackRate) < PlaybackRateTolerance)
        {
            return;
        }

        _pendingSpeed = newValue;
        _speedPlaybackValidity = ViewModel.CapturePlaybackValidity();
        _speedChangeTimer.Stop();
        _speedChangeTimer.Start();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateLayoutState();

    private void OnLayoutUpdated(object? sender, object e) => UpdateLayoutState();

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateLayoutState();

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewModel.Player.IsCompactOverlay))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isOverlayActive)
                {
                    UpdateLayoutState();
                }
            });
        }
    }

    private OverlayControlMode CurrentMode => OverlayControlLayout.SelectMode(
        ViewModel?.Player.IsCompactOverlay == true, XamlRoot?.Size.Width ?? ActualWidth, XamlRoot?.Size.Height ?? ActualHeight);

    public bool ContainsInteractivePoint(Windows.Foundation.Point point)
        => OverlayControlLayout.Contains(CurrentMode, XamlRoot?.Size.Width ?? ActualWidth,
            XamlRoot?.Size.Height ?? ActualHeight, point.X, point.Y);

    private void UpdateLayoutState()
    {
        if (!_isOverlayActive || ViewModel is null || RootGrid is not { IsLoaded: true, ActualWidth: > 0 }) return;
        // A restored window can finish its initial resize before Loaded/bindings.
        // Reconcile from arranged geometry, both on first layout and later resizes.
        var width = RootGrid.ActualWidth;
        var height = XamlRoot?.Size.Height ?? ActualHeight;
        var mode = OverlayControlLayout.SelectMode(ViewModel.Player.IsCompactOverlay, width, height);
        var layout = (Mode: mode, Width: width, Height: height, Danmaku: ViewModel.IsDanmakuControlVisible);
        if (_appliedLayout == layout) return;
        var state = OverlayControlLayout.StateName(mode, width, layout.Danmaku);
        if (!VisualStateManager.GoToState(this, state, false)) return;
        _appliedLayout = layout;
        // Constrain content, not merely popup placement. The visual state owns
        // preferred dimensions and orientation; these limits only reflect the viewport.
        var maxWidth = Math.Max(1, width - 16);
        var maxHeight = Math.Max(1, height - 16);
        foreach (var content in new FrameworkElement[] { VolumeContent, SpeedContent, ChapterContent, SubtitleContent, DanmakuContent })
            content.MaxWidth = maxWidth;
        foreach (var popup in new ScrollViewer[] { VolumePopup, SpeedPopup, ChapterPopup, SubtitlePopup, DanmakuPopup })
            popup.MaxHeight = maxHeight;
        if (mode != OverlayControlMode.CompactPortrait) BalanceLeftRightWidth();
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
        var lastSpeed = _pendingSpeed;
        _pendingSpeed = null;
        if (_isOverlayActive && isCurrent?.Invoke() == true && lastSpeed is { } speed
            && Math.Abs(speed - ViewModel.Player.PlaybackRate) >= PlaybackRateTolerance)
        {
            if (await ViewModel.ChangeSpeedAsync(speed) && isCurrent?.Invoke() == true)
            {
                CheckSpeedButtonState();
            }
        }
    }

    private void OnSubtitleFlyoutClosed(object sender, object e)
    {
        ViewModel.IsPopupVisible = false;
        // 菜单关闭后复位层级，下次打开从第一层开始。
        ViewModel.IsSubtitleParameterVisible = false;
    }

    private void OnRightPanelSizeChanged(object sender, SizeChangedEventArgs e)
        => BalanceLeftRightWidth();

    private void CheckSpeedButtonState()
    {
        if (ViewModel is null)
        {
            return;
        }

        ApplySpeedSelection(ViewModel.Player.PlaybackRate);
    }

    private void ApplySpeedSelection(double speed)
    {
        HalfSpeedButton.IsChecked = Math.Abs(speed - 0.5) < PlaybackRateTolerance;
        DefaultSpeedButton.IsChecked = Math.Abs(speed - 1) < PlaybackRateTolerance;
        OneTwoFiveSpeedButton.IsChecked = Math.Abs(speed - 1.25) < PlaybackRateTolerance;
        OneHalfSpeedButton.IsChecked = Math.Abs(speed - 1.5) < PlaybackRateTolerance;
        DoubleSpeedButton.IsChecked = Math.Abs(speed - 2) < PlaybackRateTolerance;
        TripleSpeedButton.IsChecked = Math.Abs(speed - 3) < PlaybackRateTolerance;
    }

    private async void OnQuickSpeedButtonClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsSessionCurrent())
        {
            return;
        }

        var isCurrent = ViewModel.CapturePlaybackValidity();
        var speed = Convert.ToDouble((sender as FrameworkElement)?.Tag);
        _pendingSpeed = null;
        _speedPlaybackValidity = null;
        _speedChangeTimer.Stop();
        // A preset click selects exactly one item immediately. The player's
        // property notification remains authoritative; a rejected request rolls back.
        ApplySpeedSelection(speed);
        if (Math.Abs(speed - ViewModel.Player.PlaybackRate) >= PlaybackRateTolerance)
        {
            if (!await ViewModel.ChangeSpeedAsync(speed) && isCurrent())
            {
                CheckSpeedButtonState();
            }
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
