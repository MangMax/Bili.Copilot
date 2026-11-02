// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Richasy.MpvKernel.Core.Enums;

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
    private bool _isFirstChangeVolume = true;
    private bool _isOverlayActive;

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

        BalanceLeftRightWidth();
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
        // 关闭/换集之后到达的点击只属于旧会话。
        if (!ViewModel.IsSessionCurrent())
        {
            return;
        }

        if (ViewModel.Player.PlaybackState is MpvPlayerState.Playing)
        {
            await ViewModel.Client!.PauseAsync();
        }
        else if (ViewModel.Player.PlaybackState is MpvPlayerState.Paused)
        {
            await ViewModel.Client!.ResumeAsync();
        }
        else if (ViewModel.Player.PlaybackState is MpvPlayerState.End)
        {
            await ViewModel.Player.ReplayAsync();
        }
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
    {
        var width = e.NewSize.Width;
        var miniWidth = ViewModel.IsDanmakuControlVisible ? 900 : 760;
        if (width < miniWidth)
        {
            VisualStateManager.GoToState(this, nameof(NarrowState), false);
        }
        else if (width < 1100)
        {
            VisualStateManager.GoToState(this, nameof(MediumState), false);
        }
        else if (width < 1280)
        {
            VisualStateManager.GoToState(this, nameof(BelowWideState), false);
        }
        else
        {
            VisualStateManager.GoToState(this, nameof(WideState), false);
        }

        BalanceLeftRightWidth();
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
    {
        ViewModel.IsPopupVisible = false;
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
