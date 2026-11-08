// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Richasy.MpvKernel.Core.Enums;
using Richasy.WinUIKernel.Share.Base;
using System.ComponentModel;

namespace BiliCopilot.UI.Controls.Core;

public sealed partial class PlayerOverlay : PlayerControlBase
{
    private const int HeaderBarHeight = 70;
    private readonly DispatcherQueueTimer _controlTimer;
    private readonly DispatcherQueueTimer _cursorTimer;
    private bool _isTouch;
    private bool _isInTransportControls;
    private bool _isInTitleBar;
    private Point _lastPointerPosition;

    public PlayerOverlay()
    {
        InitializeComponent();
        _controlTimer = DispatcherQueue.CreateTimer();
        _controlTimer.Interval = TimeSpan.FromMilliseconds(1500);
        _controlTimer.Tick += OnControlTimerTick;
        _cursorTimer = DispatcherQueue.CreateTimer();
        _cursorTimer.Interval = TimeSpan.FromMilliseconds(500);
        _cursorTimer.Tick += OnCursorTimerTick;
    }

    public void ShowTip(string text, InfoType type = InfoType.Error)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var popup = new TipPopup() { Text = text };
            TipContainer.Visibility = Visibility.Visible;
            TipContainer.Children.Add(popup);
            await popup.ShowAsync(type);
            TipContainer.Children.Remove(popup);
            TipContainer.Visibility = Visibility.Collapsed;

        });
    }

    internal static Visibility IsTopMostButtonVisible(bool isFullScreen, bool isAot)
        => (isFullScreen || isAot) ? Visibility.Collapsed : Visibility.Visible;

    protected override void OnControlLoaded()
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.WarningOccurred += OnWarningOccurred;
        ViewModel.RequestShowNextTip += OnRequestShowNextTip;
        ViewModel.RequestHideNextTip += OnRequestHideNextTip;
        ViewModel.RequestShowSponsorBlockTip += OnRequestShowSponsorBlockTip;
        ViewModel.RequestHideSponsorBlockTip += OnRequestHideSponsorBlockTip;
        // 控制区自己拥有显隐动画；首次加载直接对齐当前状态，不播放入场动画.
    }

    protected override void OnControlUnloaded()
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.WarningOccurred -= OnWarningOccurred;
        ViewModel.RequestShowNextTip -= OnRequestShowNextTip;
        ViewModel.RequestHideNextTip -= OnRequestHideNextTip;
        ViewModel.RequestShowSponsorBlockTip -= OnRequestShowSponsorBlockTip;
        ViewModel.RequestHideSponsorBlockTip -= OnRequestHideSponsorBlockTip;

        // 卸载后不再有事件驱动提示，主动收起以停掉它的计时器.
        SponsorBlockTip?.Hide();
        _controlTimer.Tick -= OnControlTimerTick;
        _controlTimer.Stop();
        _cursorTimer.Tick -= OnCursorTimerTick;
        _cursorTimer.Stop();
        SourceListView?.ItemsSource = default;
    }

    private void OnRequestHideNextTip(object? sender, EventArgs e)
        => NextTip?.Hide();

    private void OnRequestShowNextTip(object? sender, EventArgs e)
        => NextTip?.Show();

    private void OnRequestHideSponsorBlockTip(object? sender, EventArgs e)
        => SponsorBlockTip?.Hide();

    private void OnRequestShowSponsorBlockTip(object? sender, EventArgs e)
        => SponsorBlockTip?.Show();

    private void OnWarningOccurred(object? sender, string e)
    {
        System.Diagnostics.Debug.WriteLine($"Warning: {e}");
        //var popup = new TipPopup() { Text = e };
        //RootGrid.Children.Add(popup);
        //await popup.ShowAsync(InfoType.Warning);
        //RootGrid.Children.Remove(popup);
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;
        if (position.Equals(_lastPointerPosition))
        {
            // If the pointer hasn't moved, do nothing.
            return;
        }

        _lastPointerPosition = position;
        ViewModel.Window?.ShowCursor();
        _isTouch = e.Pointer.PointerDeviceType is Microsoft.UI.Input.PointerDeviceType.Touch or Microsoft.UI.Input.PointerDeviceType.Pen;
        _cursorTimer.Stop();
        _controlTimer.Stop();
        TestRect.Visibility = Visibility.Visible;
        // 弹层（倍速/字幕/章节/音量/弹幕设置）打开期间指针离开控制区也不能把控件收起：
        // 控件一旦 Collapsed，挂在它上面的 Flyout 会跟着关闭，正在操作的面板就被抢走了.
        if (!_isTouch && !ViewModel.IsExtraPanelVisible && !ViewModel.IsPopupVisible)
        {
            _isInTitleBar = position.Y <= HeaderBarHeight && position.Y >= 0;
            _isInTransportControls = TransportControls?.ContainsInteractivePoint(position) == true;
            if (_isInTitleBar || _isInTransportControls)
            {
                // If the pointer is in the title bar or transport controls, show controls immediately.
                _cursorTimer.Start();
                ViewModel.IsControlsVisible = true;
            }
            else
            {
                _controlTimer.Start();
                if (!ViewModel.IsEnd)
                {
                    ViewModel.IsControlsVisible = false;
                }
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsControlsVisible))
        {
            if (!ViewModel.IsControlsVisible && !_controlTimer.IsRunning)
            {
                _controlTimer.Start();
            }
            else if (!_cursorTimer.IsRunning)
            {
                _cursorTimer.Start();
            }
        }
        else if (e.PropertyName == nameof(ViewModel.CurrentVolume))
        {
            var thumbHeight = VolumeContainer.ActualHeight * (ViewModel.CurrentVolume / ViewModel.MaxVolume);
            VolumeThumb?.Height = Math.Max(0, thumbHeight);
        }
    }

    private void OnControlTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_isTouch)
        {
            return;
        }

        _controlTimer.Stop();
        TestRect.Visibility = Visibility.Collapsed;
        ViewModel.Window?.HideCursor();
    }

    private void OnCursorTimerTick(DispatcherQueueTimer sender, object args)
    {
        // 弹出面板本身是独立 popup，指针进入它时已不在主窗口矩形内，
        // 但此时不能把控件收起，否则面板会跟着关闭.
        if (!IsCursorInWindow() && !_isTouch && !ViewModel.IsPopupVisible)
        {
            ViewModel.IsControlsVisible = false;
            _cursorTimer.Stop();
        }
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        => ViewModel.Close();

    private void OnCloseTipButtonClick(object sender, RoutedEventArgs e)
        => ViewModel.ErrorMessage = default;

    private void OnBackRequested(object sender, EventArgs e)
        => ViewModel.BackCommand.Execute(default);

    private void OnHeaderBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width == 0)
        {
            return;
        }

        try
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (e.NewSize.Width < 700)
                {
                    VisualStateManager.GoToState(this, nameof(NarrowState), false);
                    HeaderBar.TitleMaxWidth = e.NewSize.Width > 200 ? e.NewSize.Width - 200 : 120;
                }
                else
                {
                    VisualStateManager.GoToState(this, nameof(DefaultState), false);
                    HeaderBar.TitleMaxWidth = e.NewSize.Width - 520;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnHeaderBarSizeChanged: {ex.Message}");
        }
    }

    private bool IsCursorInWindow()
    {
        var isSuccess = PInvoke.GetCursorPos(out var point);
        if (!isSuccess)
        {
            return false;
        }

        var window = ViewModel.Window?.GetWindow();
        if (window?.IsVisible != true)
        {
            return false;
        }

        var handle = ViewModel.Window!.Handle;
        isSuccess = PInvoke.GetClientRect(new(handle), out var clientRect);
        if (!isSuccess)
        {
            return false;
        }

        var lt = new System.Drawing.Point(clientRect.X, clientRect.Y);
        isSuccess = PInvoke.ClientToScreen(new(handle), ref lt);
        if (!isSuccess)
        {
            return false;
        }

        var rb = new System.Drawing.Point(clientRect.X + clientRect.Width, clientRect.Y + clientRect.Height);
        isSuccess = PInvoke.ClientToScreen(new(handle), ref rb);
        if (!isSuccess)
        {
            return false;
        }

        return point.X >= lt.X && point.X <= rb.X && point.Y >= lt.Y && point.Y <= rb.Y;
    }

#pragma warning disable CA1822
    private string GetTimeText(double seconds)
        => TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");

    private string GetVolumeText(double volume)
        => Math.Round(volume).ToString();

    private string GetSpeedText(double speed)
        => $"{Math.Round(speed, 2)}x";
#pragma warning restore CA1822

    internal static bool IsPaused(MpvPlayerState state)
    {
        return state is MpvPlayerState.Paused;
    }

    internal static bool IsLoading(MpvPlayerState state, bool isLoading)
    {
        return (state is MpvPlayerState.Seeking or MpvPlayerState.Buffering) || isLoading;
    }

    internal static Visibility IsVolumeIndicatorVisible(bool isControlVisible, bool isVolumeChanging)
    {
        return !isControlVisible && isVolumeChanging ? Visibility.Visible : Visibility.Collapsed;
    }

    internal static Visibility IsBottomProgressShouldDisplay(bool isAot, bool isFullScreen, bool isLoading, MpvPlayerState state)
    {
        if ((isAot || isFullScreen) && !IsLoading(state, isLoading))
        {
            return Visibility.Collapsed;
        }

        return Visibility.Visible;
    }

    private void OnSourceItemClick(object sender, RoutedEventArgs e)
    {
        var selectedSource = (sender as FrameworkElement)?.DataContext as SourceItemViewModel;
        selectedSource?.ActiveCommand.Execute(default);
        SourceFlyout?.Hide();
    }
}
