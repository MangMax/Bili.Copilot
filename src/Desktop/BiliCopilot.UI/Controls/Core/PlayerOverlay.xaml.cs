// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
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
    private bool _isSendingDetailsDanmaku;
    private bool _useDefaultLayout;
    private long _splitterHideToken;
    private long _splitterWidthToken;
    private OverlayPresentation _presentation;
    private Point _lastPointerPosition;

    public PlayerOverlay()
    {
        InitializeComponent();
        // 初始布局：按用户偏好（记忆上次/总是铺满/总是默认布局）决定.
        var layoutPreference = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.PlayerLayoutPreference, Models.Constants.PlayerLayoutPreference.Remember);
        var lastUseDefaultLayout = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.LastPlayerUseDefaultLayout, false);
        _useDefaultLayout = BiliCopilot.UI.Player.PlayerWindowPreferenceResolver.ResolveInitialDefaultLayout(layoutPreference, lastUseDefaultLayout);
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

    internal static float GetTopMostIconRotation(bool isTopMost)
        => isTopMost ? 35f : 0f;

    internal static double GetTopMostOutlineOpacity(bool isTopMost)
        => isTopMost ? 0d : 1d;

    internal static double GetTopMostFilledOpacity(bool isTopMost)
        => isTopMost ? 1d : 0d;

    protected override void OnControlLoaded()
    {
        AttachTitleBar();
        _splitterHideToken = DetailsSidebarToggle.RegisterPropertyChangedCallback(VisibilityToggleButton.IsHideProperty, OnDetailsSplitterPropertyChanged);
        _splitterWidthToken = DetailsSplitter.RegisterPropertyChangedCallback(PropertySizer.BindingProperty, OnDetailsSplitterPropertyChanged);
        UpdatePlaybackLayout();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.WarningOccurred += OnWarningOccurred;
        ViewModel.RequestShowNextTip += OnRequestShowNextTip;
        ViewModel.RequestHideNextTip += OnRequestHideNextTip;
        ViewModel.RequestShowSponsorBlockTip += OnRequestShowSponsorBlockTip;
        ViewModel.RequestHideSponsorBlockTip += OnRequestHideSponsorBlockTip;
    }

    protected override void OnControlUnloaded()
    {
        DetachTitleBar();
        DetailsSidebarToggle.UnregisterPropertyChangedCallback(VisibilityToggleButton.IsHideProperty, _splitterHideToken);
        DetailsSplitter.UnregisterPropertyChangedCallback(PropertySizer.BindingProperty, _splitterWidthToken);
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
            var videoPosition = e.GetCurrentPoint(VideoViewport).Position;
            _isInTitleBar = !_presentation.UsesDefaultLayout && videoPosition.Y >= 0 && videoPosition.Y <= HeaderBarHeight
                && videoPosition.X >= 0 && videoPosition.X <= VideoViewport.ActualWidth;
            _isInTransportControls = TransportControls?.ContainsInteractivePoint(
                videoPosition, VideoViewport.ActualWidth, VideoViewport.ActualHeight) == true;
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
            UpdateHeaderVisibility();
            UpdateTitleBarRegions();
            if (!ViewModel.IsControlsVisible && !_controlTimer.IsRunning)
            {
                _controlTimer.Start();
            }
            else if (!_cursorTimer.IsRunning)
            {
                _cursorTimer.Start();
            }
        }
        else if (e.PropertyName == nameof(ViewModel.IsExtraPanelVisible))
        {
            UpdateTitleBarRegions();
        }
        else if (e.PropertyName == nameof(ViewModel.Connector))
        {
            UpdatePlaybackLayout();
        }
        else if (e.PropertyName == nameof(ViewModel.CurrentVolume))
        {
            var thumbHeight = VolumeContainer.ActualHeight * (ViewModel.CurrentVolume / ViewModel.MaxVolume);
            VolumeThumb?.Height = Math.Max(0, thumbHeight);
        }
    }

    private void OnLayoutToggleRequested(object? sender, EventArgs e)
    {
        _useDefaultLayout = !_useDefaultLayout;
        // 记住用户手动选择的布局，供“记忆上次”初始布局偏好读取.
        SettingsToolkit.WriteLocalSetting(Models.Constants.SettingNames.LastPlayerUseDefaultLayout, _useDefaultLayout);
        ViewModel.IsExtraPanelVisible = false;
        UpdatePlaybackLayout();
    }

    private void UpdatePlaybackLayout()
    {
        UpdatePresentation();
        var detailsVisibility = _presentation.UsesDefaultLayout ? Visibility.Visible : Visibility.Collapsed;
        DetailsBackground.Visibility = DetailsLeftGutter.Visibility = DetailsRightGutter.Visibility = detailsVisibility;
        DetailsDanmakuBar.Visibility = detailsVisibility;
        VideoViewportFrame.CornerRadius = new CornerRadius(_presentation.UsesDefaultLayout ? 8 : 0);
        VideoCornerMask.Visibility = detailsVisibility;
        VideoCornerMask.Margin = VideoViewportFrame.Margin = _presentation.UsesDefaultLayout ? new Thickness(16, 54, 16, 0) : new Thickness(0);
        OverlayTitle.Visibility = PlaybackWidget.Visibility = _presentation.UsesDefaultLayout ? Visibility.Collapsed : Visibility.Visible;
        UpdateDetailsSidebar();
        HeaderQuality.Visibility = _presentation.UsesDefaultLayout ? Visibility.Collapsed : Visibility.Visible;
        DetailsCompactButton.Visibility = detailsVisibility;
        DetailsWindowTitle.Visibility = detailsVisibility;
        HeaderGradient.Visibility = _presentation.UsesDefaultLayout ? Visibility.Collapsed : Visibility.Visible;
        UpdateHeaderVisibility();
        UpdateTitleBarRegions();
        UpdateVideoViewport();
    }

    private void UpdatePresentation()
    {
        var window = ViewModel?.Window?.GetWindow().Presenter.Kind switch
        {
            AppWindowPresenterKind.CompactOverlay => OverlayWindowMode.Compact,
            AppWindowPresenterKind.FullScreen => OverlayWindowMode.FullScreen,
            AppWindowPresenterKind.Overlapped => OverlayWindowMode.Overlapped,
            _ => OverlayWindowMode.Other,
        };
        _presentation = OverlayControlLayout.Resolve(window,
            ViewModel?.Connector is VideoConnectorViewModel or PgcConnectorViewModel,
            _useDefaultLayout, RootGrid.ActualWidth, RootGrid.ActualHeight);
        TransportControls.ApplyPresentation(_presentation);
    }

    private void OnPlayerRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePresentation();
        UpdateDetailsSidebar();
    }

    private void OnDetailsSplitterPropertyChanged(DependencyObject sender, DependencyProperty property)
        => UpdateDetailsSidebar();

    private void UpdateDetailsSidebar()
    {
        var width = OverlayControlLayout.GetSidebarWidth(_presentation, RootGrid.ActualWidth, RootGrid.ActualHeight,
            DetailsSplitter.Minimum, DetailsSplitter.Binding);
        var canShowSidebar = width > 0;
        var showSidebar = canShowSidebar && !DetailsSidebarToggle.IsHide;
        DetailsSidebarToggleHost.Visibility = canShowSidebar ? Visibility.Visible : Visibility.Collapsed;
        DetailsSplitter.Visibility = DetailsSidebarSurface.Visibility = DetailsSidebar.Visibility =
            showSidebar ? Visibility.Visible : Visibility.Collapsed;
        DetailsColumn.Width = new GridLength(showSidebar ? width : 0);
        DockedSidebarContent.Content = showSidebar ? ViewModel?.Connector : null;
    }

    private void UpdateHeaderVisibility()
    {
        HeaderBar.Height = _presentation.UsesDefaultLayout ? 48 : 44;
        HeaderOverlay.Height = _presentation.UsesDefaultLayout ? HeaderBar.Height : 60;
        HeaderOverlay.Visibility = _presentation.UsesDefaultLayout || ViewModel.IsControlsVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnDetailsDanmakuTextChanged(object sender, TextChangedEventArgs e)
        => UpdateDetailsDanmakuSendButton();

    private async void OnDetailsDanmakuSendClick(object sender, RoutedEventArgs e)
        => await SendDetailsDanmakuAsync();

    private async void OnDetailsDanmakuKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await SendDetailsDanmakuAsync();
        }
    }

    private async Task SendDetailsDanmakuAsync()
    {
        if (_isSendingDetailsDanmaku || string.IsNullOrWhiteSpace(DetailsDanmakuInput.Text))
        {
            return;
        }

        var text = DetailsDanmakuInput.Text;
        _isSendingDetailsDanmaku = true;
        UpdateDetailsDanmakuSendButton();
        try
        {
            if (await ViewModel.TrySendInlineDanmakuAsync(text.Trim()) && DetailsDanmakuInput.Text == text)
            {
                DetailsDanmakuInput.Text = string.Empty;
            }
        }
        finally
        {
            _isSendingDetailsDanmaku = false;
            UpdateDetailsDanmakuSendButton();
        }
    }

    private void UpdateDetailsDanmakuSendButton()
        => DetailsDanmakuSendButton.IsEnabled = !_isSendingDetailsDanmaku && !string.IsNullOrWhiteSpace(DetailsDanmakuInput.Text);

    private void OnVideoViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        VideoViewport.Clip = new RectangleGeometry { Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        UpdateVideoViewport();
    }

    private void UpdateVideoViewport()
    {
        var scale = XamlRoot?.RasterizationScale ?? 1d;
        var margin = VideoViewportFrame.Margin;
        ViewModel?.Window?.SetVideoViewport(_presentation.UsesDefaultLayout
            ? new Rect(margin.Left * scale, margin.Top * scale, VideoViewport.ActualWidth * scale, VideoViewport.ActualHeight * scale)
            : null);
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

    private void OnSourceFlyoutOpened(object sender, object e) => ViewModel.IsPopupVisible = true;

    private void OnSourceFlyoutClosed(object sender, object e) => ViewModel.IsPopupVisible = false;

    private void OnSourceItemClick(object sender, RoutedEventArgs e)
    {
        var selectedSource = (sender as FrameworkElement)?.DataContext as SourceItemViewModel;
        selectedSource?.ActiveCommand.Execute(default);
        if (sender is FrameworkElement element)
        {
            CloseAncestorFlyout(element);
        }
    }

    private void OnLiveQualityItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element
            || ViewModel?.LiveConnector is not { } connector
            || element.DataContext is not LiveQualityOption option)
        {
            return;
        }

        connector.SelectedQuality = option;
        CloseAncestorFlyout(element);
    }

    private void OnLiveLineItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element
            || ViewModel?.LiveConnector is not { } connector
            || element.DataContext is not LiveLineOption option)
        {
            return;
        }

        connector.SelectedLine = option;
        CloseAncestorFlyout(element);
    }

    private static void CloseAncestorFlyout(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is Popup popup)
            {
                popup.IsOpen = false;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }
}
