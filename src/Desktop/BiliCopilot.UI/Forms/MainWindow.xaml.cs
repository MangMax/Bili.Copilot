// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.Toolkits;
using Windows.Graphics;
using WinUIEx;

namespace BiliCopilot.UI.Forms;

/// <summary>
/// 主窗口.
/// </summary>
public sealed partial class MainWindow : WindowBase, ITipWindow
{
    private const int WindowMinWidth = 640;
    private const int WindowMinHeight = 480;
    private bool _isFirstActivated = true;
    private bool _forceClose;
    private InputKeyboardSource? _keyboardSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        SetTitleBar(RootLayout.GetMainTitleBar());
        Title = ResourceToolkit.GetLocalizedString(StringNames.AppName);
        this.SetIcon("Assets/logo.ico");
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        MinWidth = WindowMinWidth;
        MinHeight = WindowMinHeight;
        this.Get<AppViewModel>().Windows.Add(this);
        Activated += OnActivated;
        Closed += OnClosed;
        ((App)Application.Current).InitializeTrayLifecycle();
    }

    /// <inheritdoc/>
    public async Task ShowTipAsync(string text, InfoType type = InfoType.Error)
    {
        var popup = new TipPopup() { Text = text };
        TipContainer.Visibility = Visibility.Visible;
        TipContainer.Children.Add(popup);
        await popup.ShowAsync(type);
        TipContainer.Children.Remove(popup);
        TipContainer.Visibility = Visibility.Collapsed;
    }

    private static PointInt32 GetSavedWindowPosition()
    {
        var left = SettingsToolkit.ReadLocalSetting(SettingNames.MainWindowPositionLeft, 0);
        var top = SettingsToolkit.ReadLocalSetting(SettingNames.MainWindowPositionTop, 0);
        return new PointInt32(left, top);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            this.Get<AppViewModel>().ActivatedWindow = this;
        }

        if (!_isFirstActivated)
        {
            return;
        }

        MoveAndResize();
        this.Get<ILogger<App>>().LogInformation($"App version: {this.Get<IAppToolkit>().GetPackageVersion()}");
        var localTheme = SettingsToolkit.ReadLocalSetting(SettingNames.AppTheme, ElementTheme.Default);
        this.Get<AppViewModel>().ChangeThemeCommand.Execute(localTheme);
        AppWindow.Changed += OnWindowChanged;
        _isFirstActivated = false;
    }

    internal void UnregisterEventHandlers()
    {
        Activated -= OnActivated;
        Closed -= OnClosed;
        AppWindow.Changed -= OnWindowChanged;
        if (_keyboardSource is not null)
        {
            _keyboardSource.KeyUp -= OnWindowKeyUp;
            _keyboardSource = null;
        }
    }

    internal async Task<bool> RequestExitAsync()
    {
        var app = (App)Application.Current;
        var appVM = this.Get<AppViewModel>();
        try
        {
            await app.TrayLifecycle!.ExitAsync(async () =>
            {
                appVM.IsClosed = true;
                // Even a synchronous save must let the canceled native close event return first.
                await Task.Yield();
                SaveCurrentWindowStats();
                await this.Get<SettingsPageViewModel>().CheckSaveServicesAsync();
            }, async () =>
            {
                // CloseAsync includes an already running native-close cleanup.
                await Task.WhenAll(appVM.Players.ToArray().Select(player => player.CloseAsync()));
            }, () =>
            {
                app.ReleaseTrayResources();
                RootLayout.ViewModel.Back();
                foreach (var window in appVM.Windows.ToArray())
                {
                    if (window is not MainWindow)
                    {
                        window.Close();
                    }
                }

                appVM.Windows.Remove(this);
                UnregisterEventHandlers();
                Close();
                app.Exit();
            });

            return true;
        }
        catch (Exception ex)
        {
            this.Get<ILogger<App>>().LogError(ex, "退出应用时失败");
            return false;
        }
    }

    private async void OnClosed(object sender, WindowEventArgs e)
    {
        if (_forceClose)
        {
            // The unified exit failed; let the native close proceed so the window is not stuck.
            return;
        }

        // Window.Closed has no async deferral: cancel native destruction before any await.
        e.Handled = true;
        var appVM = this.Get<AppViewModel>();
        var app = (App)Application.Current;
        if (appVM.IsClosed)
        {
            return;
        }

        if (app.TrayLifecycle?.HandleClose() == true)
        {
            return;
        }

        var hasPlayer = appVM.Players.Count > 0;
        if (hasPlayer)
        {
            appVM.HideAllWindowsCommand.Execute(default);
            return;
        }

        if (!await RequestExitAsync())
        {
            _forceClose = true;
            Close();
        }
    }

    private void OnWindowKeyUp(InputKeyboardSource sender, KeyEventArgs args)
    {
        if (args.VirtualKey == Windows.System.VirtualKey.F && AppToolkit.IsOnlyCtrlPressed())
        {
            RootLayout.TryFocusSearchBox();
        }
    }

    private RectInt32 GetRenderRect(DisplayArea area)
    {
        var workArea = area.WorkArea;
        var scaleFactor = HwndExtensions.GetDpiForWindow(this.GetWindowHandle()) / 96d;
        var previousWidth = SettingsToolkit.ReadLocalSetting(SettingNames.MainWindowWidth, 1280d);
        var previousHeight = SettingsToolkit.ReadLocalSetting(SettingNames.MainWindowHeight, 760d);
        var width = Convert.ToInt32(previousWidth * scaleFactor);
        var height = Convert.ToInt32(previousHeight * scaleFactor);

        // 如果宽高比为 16 : 9，则计算出窗口实际宽度。
        double maxPageWidth;
        if (area.OuterBounds.Width * 9 == area.OuterBounds.Height * 16)
        {
            var maxWindowWidth = area.OuterBounds.Width / scaleFactor;
            maxPageWidth = maxWindowWidth - 74;
        }
        else
        {
            // 取当前高度的 16 : 9 比例宽度。
            var maxWindowWidth = area.OuterBounds.Height * 16 / 9 / scaleFactor;
            maxPageWidth = maxWindowWidth - 74;
        }

        App.Current.Resources["MaxPageWidth"] = maxPageWidth;

        // Ensure the window is not larger than the work area.
        if (height > workArea.Height - 20)
        {
            height = workArea.Height - 20;
        }

        var lastPoint = GetSavedWindowPosition();
        var isZeroPoint = lastPoint.X == 0 && lastPoint.Y == 0;
        var isValidPosition = lastPoint.X >= workArea.X && lastPoint.Y >= workArea.Y && lastPoint.X + width <= workArea.X + workArea.Width && lastPoint.Y + height <= workArea.Y + workArea.Height;
        var left = isZeroPoint || !isValidPosition
            ? (workArea.Width - width) / 2d
            : lastPoint.X;
        var top = isZeroPoint || !isValidPosition
            ? (workArea.Height - height) / 2d
            : lastPoint.Y;
        return new RectInt32(Convert.ToInt32(left), Convert.ToInt32(top), width, height);
    }

    private void MoveAndResize()
    {
        var lastPoint = GetSavedWindowPosition();
        var displayArea = DisplayArea.GetFromPoint(lastPoint, DisplayAreaFallback.Primary)
            ?? DisplayArea.Primary;
        var rect = GetRenderRect(displayArea);
        AppWindow.MoveAndResize(rect);
        var isMaximized = SettingsToolkit.ReadLocalSetting(SettingNames.IsMainWindowMaximized, false);
        if (isMaximized)
        {
            (AppWindow.Presenter as OverlappedPresenter)?.Maximize();
        }
    }

    private void SaveCurrentWindowStats()
    {
        var left = AppWindow.Position.X;
        var top = AppWindow.Position.Y;
        var isMaximized = PInvoke.IsZoomed(new HWND(this.GetWindowHandle()));
        SettingsToolkit.WriteLocalSetting(SettingNames.IsMainWindowMaximized, (bool)isMaximized);

        if (!isMaximized)
        {
            SettingsToolkit.WriteLocalSetting(SettingNames.MainWindowPositionLeft, left);
            SettingsToolkit.WriteLocalSetting(SettingNames.MainWindowPositionTop, top);

            if (Height >= WindowMinHeight && Width >= WindowMinWidth)
            {
                SettingsToolkit.WriteLocalSetting(SettingNames.MainWindowHeight, Height);
                SettingsToolkit.WriteLocalSetting(SettingNames.MainWindowWidth, Width);
            }
        }
    }

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPositionChange)
        {
            SaveCurrentWindowStats();
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint((UIElement)sender);
        if (point.Properties.IsXButton1Pressed || point.Properties.IsXButton2Pressed)
        {
            e.Handled = true;
            if (RootLayout.ViewModel.IsOverlayOpen)
            {
                RootLayout.ViewModel.Back();
            }
        }
    }

    private void OnRootLayoutLoaded(object sender, RoutedEventArgs e)
    {
        if (_keyboardSource is null)
        {
            _keyboardSource = InputKeyboardSource.GetForIsland(RootLayout.XamlRoot?.ContentIsland);
            _keyboardSource.KeyUp += OnWindowKeyUp;
        }
    }

    private bool IsCursorInWindow()
    {
        var isSuccess = PInvoke.GetCursorPos(out var point);
        if (!isSuccess)
        {
            return false;
        }

        if (AppWindow is null || !AppWindow.IsVisible)
        {
            return false;
        }

        var handle = this.GetWindowHandle();
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

    private bool IsForegroundWindow()
    {
        var handle = PInvoke.GetForegroundWindow();
        return handle.Equals(new(this.GetWindowHandle()));
    }
}
