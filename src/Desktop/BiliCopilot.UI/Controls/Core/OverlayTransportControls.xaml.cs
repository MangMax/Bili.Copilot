// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Icons;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Automation;
using Richasy.MpvKernel.Core.Enums;
using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.Controls.Core;

public sealed partial class OverlayTransportControls : PlayerControlBase
{
    public static readonly DependencyProperty LeftElementProperty =
        DependencyProperty.Register(nameof(LeftElement), typeof(object), typeof(OverlayTransportControls), new PropertyMetadata(default));

    public static readonly DependencyProperty IsSkipButtonsVisibleProperty =
        DependencyProperty.Register(nameof(IsSkipButtonsVisible), typeof(bool), typeof(OverlayTransportControls), new PropertyMetadata(true));

    private const double PlaybackRateTolerance = 0.01;

    private double? _pendingSpeed;
    private Func<bool>? _speedPlaybackValidity;
    private readonly DispatcherTimer _speedChangeTimer;
    private OverlayPresentation _presentation;

    public static readonly DependencyProperty DetailsQualityElementProperty =
        DependencyProperty.Register(nameof(DetailsQualityElement), typeof(object), typeof(OverlayTransportControls), new PropertyMetadata(null));

    public object? DetailsQualityElement
    {
        get => GetValue(DetailsQualityElementProperty);
        set => SetValue(DetailsQualityElementProperty, value);
    }
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

    public event EventHandler? PlaybackLayoutToggleRequested;

    internal void ApplyPresentation(OverlayPresentation presentation)
    {
        if (_presentation == presentation)
        {
            return;
        }

        _presentation = presentation;
        var text = ResourceToolkit.GetLocalizedString(presentation.UsesDefaultLayout
            ? Models.Constants.StringNames.PlayerFillLayout
            : Models.Constants.StringNames.Default);
        ToolTipService.SetToolTip(PlaybackLayoutButton, text);
        AutomationProperties.SetName(PlaybackLayoutButton, text);
        PlaybackLayoutMenuText.Text = text;
        var template = (DataTemplate)Resources[presentation.UsesDefaultLayout ? "PlayerFillLayoutIconTemplate" : "PlayerDefaultLayoutIconTemplate"];
        PlaybackLayoutIcon.ContentTemplate = PlaybackLayoutMenuIcon.ContentTemplate = template;
        PlaybackLayoutButton.Visibility = PlaybackLayoutMenuItem.Visibility =
            presentation.CanToggleLayout ? Visibility.Visible : Visibility.Collapsed;
        UpdateLayoutState();
    }

    private void OnPlaybackLayoutClick(object sender, RoutedEventArgs e)
        => PlaybackLayoutToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnOverflowActionClick(object sender, RoutedEventArgs e)
    {
        OverflowFlyout.Hide();
        if (ReferenceEquals(sender, PlaybackLayoutMenuItem))
        {
            OnPlaybackLayoutClick(sender, e);
            return;
        }

        var flyout = sender switch
        {
            var action when ReferenceEquals(action, OverflowVolumeAction) => VolumeFlyout,
            var action when ReferenceEquals(action, OverflowSpeedAction) => SpeedFlyout,
            var action when ReferenceEquals(action, OverflowChapterAction) => ChapterFlyout,
            _ => null,
        };
        if (flyout is not null)
        {
            DispatcherQueue.TryEnqueue(() => flyout.ShowAt(OverflowButton));
        }
    }

    public object LeftElement
    {
        get => (object)GetValue(LeftElementProperty);
        set => SetValue(LeftElementProperty, value);
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
        ProgressSlider.ViewModel = ViewModel;
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
        UpdateLayoutState();
    }

    private void OnOverlayLoaded(object sender, RoutedEventArgs e) => OnControlLoaded();

    private void OnOverlayUnloaded(object sender, RoutedEventArgs e) => OnControlUnloaded();

    protected override void OnViewModelChanged(PlayerViewModel? oldValue, PlayerViewModel? newValue)
    {
        if (ProgressSlider is not null)
        {
            ProgressSlider.ViewModel = newValue;
        }

        _appliedLayout = null;
        _speedChangeTimer.Stop();
        _pendingSpeed = null;
        _speedPlaybackValidity = null;
        if (_isOverlayActive && newValue != null)
        {
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
        _isOverlayActive = false;
        _appliedLayout = null;
        _pendingSpeed = null;
        _speedPlaybackValidity = null;
        _speedChangeTimer.Stop();
        _speedChangeTimer.Tick -= OnSpeedChangeTick;

        SubtitleRepeater?.ItemsSource = null;
    }

#pragma warning disable CA1822
    private string GetPlayPauseSymbol(MpvPlayerState state)
        => state == MpvPlayerState.Playing ? IconGlyph.PauseFilled : IconGlyph.PlayFilled;

    private string GetCompactOverlaySymbol(bool isCompactOverlay)
        => isCompactOverlay ? IconGlyph.ExitCompactOverlay : IconGlyph.EnterCompactOverlay;

    private string GetTimelineText(double position, double duration, bool isVideoPreview)
        => $"{GetTimeText(position)} / {GetTimelineDurationText(duration, isVideoPreview)}";

    private string GetFullScreenSymbol(bool isFullScreen)
        => isFullScreen ? IconGlyph.FullScreenMinimize : IconGlyph.FullScreenMaximize;

    private bool IsPlayPauseEnabled(MpvPlayerState state)
        => state is MpvPlayerState.Playing or MpvPlayerState.Paused or MpvPlayerState.End;

    private string GetPlayPauseTip(bool isPlaying)
        => isPlaying ? ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Pause) : ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Play);

    private bool IsLoading(MpvPlayerState state, bool isLoading)
        => state is MpvPlayerState.Seeking or MpvPlayerState.Buffering || isLoading;

    private bool CanSeekTimeline(bool isLoading, double duration)
        => !isLoading && double.IsFinite(duration) && duration > 0;

    private string GetTimeText(double seconds)
        => TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");

    private string GetTimelineDurationText(double seconds, bool isVideoPreview)
    {
        var duration = GetTimeText(seconds);
        return isVideoPreview
            ? string.Format(ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.VideoPreviewDuration), duration)
            : duration;
    }

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

    public bool ContainsInteractivePoint(Windows.Foundation.Point point, double width, double height)
        => ViewModel?.Player is not null
            && OverlayControlLayout.Contains(_presentation.Mode, width, height, point.X, point.Y);

    private void UpdateLayoutState()
    {
        if (!_isOverlayActive || ViewModel is null || RootGrid is not { IsLoaded: true, ActualWidth: > 0 }) return;
        // A restored window can finish its initial resize before Loaded/bindings.
        // Reconcile from arranged geometry, both on first layout and later resizes.
        var width = RootGrid.ActualWidth;
        var height = XamlRoot?.Size.Height ?? ActualHeight;
        var mode = _presentation.Mode;
        var layout = (Mode: mode, Width: width, Height: height, Danmaku: ViewModel.IsDanmakuControlVisible);
        if (_appliedLayout == layout) return;
        var states = OverlayControlLayout.SelectVisualStates(mode, width, layout.Danmaku);
        if (!VisualStateManager.GoToState(this, states.Layout, false)
            || !VisualStateManager.GoToState(this, states.Controls, false))
        {
            return;
        }

        _appliedLayout = layout;
        DetailsPlaybackTime.Visibility = OverlayControlLayout.ShowsDetailsTime(mode, width) ? Visibility.Visible : Visibility.Collapsed;
        // Constrain content, not merely popup placement. The visual state owns
        // preferred dimensions and orientation; these limits only reflect the viewport.
        var maxWidth = Math.Max(1, width - 16);
        var maxHeight = Math.Max(1, height - 16);
        // 这些元素在 danmaku 关闭 / 模板尚未加载时可能为 null；布局回调不能因此抛 NRE。
        foreach (var content in new FrameworkElement?[] { VolumeContent, SpeedContent, ChapterContent, SubtitleContent, DanmakuContent })
        {
            if (content is not null)
            {
                content.MaxWidth = maxWidth;
            }
        }

        foreach (var popup in new ScrollViewer?[] { VolumePopup, SpeedPopup, ChapterPopup, SubtitlePopup, DanmakuPopup })
        {
            if (popup is not null)
            {
                popup.MaxHeight = maxHeight;
            }
        }
        if (mode != OverlayControlMode.CompactPortrait) BalanceLeftRightWidth();
    }

    private void BalanceLeftRightWidth()
    {
        if (_presentation.UsesDefaultLayout)
        {
            return;
        }

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
        // 打开视频的最初一瞬 Player 尚未创建，滑条按 FallbackValue 触发 ValueChanged，此时不能解引用 Player。
        if (ViewModel?.Player is null)
        {
            return;
        }

        ApplySpeedSelection(ViewModel.Player.PlaybackRate);
    }

    private void ApplySpeedSelection(double speed)
    {
        // 与弹窗容器同理：模板尚未加载时按钮可能为 null。
        if (HalfSpeedButton is null)
        {
            return;
        }

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
