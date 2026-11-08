using BiliCopilot.UI.Controls.Core;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
using Richasy.WinUIKernel.Share.Base;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BiliCopilot.UI.Forms;

public sealed partial class PlayerWindow : IAsyncDisposable
{
    private const int WindowMinWidth = 480;
    private const int WindowMinHeight = 270;
    private readonly AppWindow _rootWindow;
    private readonly DesktopWindowXamlSource _xamlSource;
    private readonly CursorGrid _rootGrid;
    private readonly PlayerWindowPresentation _presentation;
    private InputActivationListener _inputActivationListener;
    private bool _xamlSourceDisposed;
    private bool _isFirstActivated = true;
    private Action<bool> _activeChangedAction;

    public PlayerWindow()
    {
        _rootWindow = AppWindow.Create();
        _rootWindow.AssociateWithDispatcherQueue(DispatcherQueue.GetForCurrentThread());
        _rootWindow.Destroying += OnWindowDestroying;

        _rootWindow.SetIcon("Assets/logo.ico");
        _rootWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        _rootWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        _rootWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        _rootWindow.TitleBar.ButtonHoverBackgroundColor = "#222222".ToColor();
        _rootWindow.TitleBar.ButtonPressedBackgroundColor = "#111111".ToColor();
        _rootWindow.TitleBar.ButtonForegroundColor = Colors.White;
        _rootWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
        _rootWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
        _rootWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        _rootGrid = new CursorGrid();
        _xamlSource = new DesktopWindowXamlSource();
        _xamlSource.Initialize(_rootWindow.Id);
        _xamlSource.Content = _rootGrid;

        _inputActivationListener = InputActivationListener.GetForWindowId(_rootWindow.Id);
        _inputActivationListener.InputActivationChanged += OnInputActivationChanged;

        ResetWindowMinSize();
        _ratio = new CompactOverlayRatio(SettingsToolkit.CompactOverlayRatio);
        _presentation = new(new PlayerWindowSurface(this), new PlayerWindowState(this), _ratio);
    }

    /// <summary>
    /// 对象是否已经被释放.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 窗口是否已关闭.
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// 窗口大小改变事件.
    /// </summary>
    public event EventHandler<Size> SizeChanged;

    /// <summary>
    /// 窗口句柄.
    /// </summary>
    public IntPtr Handle => Win32Interop.GetWindowFromWindowId(_rootWindow.Id);

    /// <summary>
    /// XAML 根元素.
    /// </summary>
    public XamlRoot? XamlRoot => _xamlSourceDisposed ? null : _xamlSource?.Content?.XamlRoot;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return ValueTask.CompletedTask;
        }

        _presentation.Close();
        _ratio.Dispose();
        IsDisposed = true;

        // Close the island while its HWND still exists: destroying the window first and
        // disposing the island afterwards faults inside CoreMessagingXP (stowed E_POINTER
        // 0x80004003 reported as 0xc000027b).
        DisposeXamlSource();

        if (_rootWindow != null)
        {
            _rootWindow.Changed -= OnWindowChanged;
            if (!IsClosed)
            {
                _rootWindow.Destroy();
            }
        }

        return ValueTask.CompletedTask;
    }

    private void DisposeXamlSource()
    {
        if (_xamlSourceDisposed)
        {
            return;
        }

        _xamlSourceDisposed = true;
        // Detach the XAML tree before closing the island so no dispatcher callback reaches
        // content that is already being torn down.
        _xamlSource.Content = null;
        _xamlSource.Dispose();
    }

    /// <summary>
    /// 显示窗口.
    /// </summary>
    public void Show(bool restorePosition = true)
    {
        _presentation.Show(restorePosition, activate: true);
        PInvoke.SetForegroundWindow(new(Handle));
        PInvoke.SetFocus(new(Handle));

        if (_isFirstActivated)
        {
            _rootWindow.Changed += OnWindowChanged;
            UpdateXamlSourcePosition();
            if (PInvoke.GetForegroundWindow() == Handle)
            {
                PInvoke.SetFocus(new(Win32Interop.GetWindowFromWindowId(_xamlSource.SiteBridge.WindowId)));
                ResetFocus();
            }

            _isFirstActivated = false;
        }
    }

    public void ShowTip(string message, InfoType type)
    {
        if (_rootGrid?.Children.FirstOrDefault() is PlayerOverlay overlay)
        {
            overlay.ShowTip(message, type);
        }
    }

    /// <summary>
    /// 设置标题栏按钮的可见性.
    /// </summary>
    /// <param name="isVisible"></param>
    public void SetCaptionButtonVisibility(bool isVisible)
    {
        _rootWindow.TitleBar?.ButtonForegroundColor = isVisible ? Colors.White : "#111111".ToColor();
    }

    public void SetActiveAction(Action<bool> activeChangedAction)
        => _activeChangedAction = activeChangedAction;

    /// <summary>
    /// 关闭窗口.
    /// </summary>
    public void Close() => _rootWindow.Destroy();

    /// <summary>
    /// 隱藏光标.
    /// </summary>
    public void HideCursor()
        => _rootGrid?.HideCursor();

    /// <summary>
    /// 显示光标.
    /// </summary>
    public void ShowCursor()
        => _rootGrid?.ShowCursor();

    public void EnterFullScreen()
    {
        if (IsClosed || IsDisposed)
        {
            return;
        }

        if (_rootWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
        {
            if (IsTopMost())
            {
                SetTopMost(false);
            }

            _ = _presentation.RequestModeAsync(WindowPresentationMode.FullScreen);
        }
    }

    public void ExitFullScreen()
    {
        if (IsClosed || IsDisposed)
        {
            return;
        }

        if (_rootWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            _ = _presentation.RequestModeAsync(WindowPresentationMode.Ordinary);
        }
    }

    public void EnterCompactOverlay()
    {
        if (IsClosed || IsDisposed)
        {
            return;
        }

        if (_rootWindow.Presenter.Kind != AppWindowPresenterKind.CompactOverlay)
        {
            if (IsTopMost())
            {
                SetTopMost(false);
            }

            _ = _presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        }
    }

    public void ExitCompactOverlay()
    {
        if (IsClosed || IsDisposed)
        {
            return;
        }

        if (_rootWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay)
        {
            _ = _presentation.RequestModeAsync(WindowPresentationMode.Ordinary);
        }
    }

    /// <summary>
    /// 设置主题.
    /// </summary>
    /// <param name="theme">元素主题.</param>
    public void SetTheme(ElementTheme theme)
    {
        _rootGrid.RequestedTheme = theme;
    }

    /// <summary>
    /// 设置UI元素.
    /// </summary>
    /// <param name="element">UI元素.</param>
    public void SetUIElement(UIElement? element)
    {
        if (_rootGrid.Children.Count > 0)
        {
            _rootGrid.Children.Clear();
        }

        if (element is not null)
        {
            _rootGrid.Children.Add(element);
        }
    }

    /// <summary>
    /// 获取窗口对象.
    /// </summary>
    /// <returns><see cref="AppWindow"/>.</returns>
    public AppWindow GetWindow()
        => _rootWindow;

    public void DelayUpdateTitleBarPadding(bool delaySec = false)
    {
        if (_rootGrid?.Children.FirstOrDefault() is PlayerOverlay overlay)
        {
            this.Get<DispatcherQueue>().TryEnqueue(async () =>
            {
                if (IsClosed || IsDisposed)
                {
                    return;
                }

                if (delaySec)
                {
                    overlay?.HeaderBar.HorizontalAlignment = HorizontalAlignment.Left;
                    await Task.Delay(800);
                    if (IsClosed || IsDisposed)
                    {
                        return;
                    }

                    overlay?.HeaderBar.HorizontalAlignment = HorizontalAlignment.Stretch;
                    await Task.Delay(800);
                    if (IsClosed || IsDisposed)
                    {
                        return;
                    }
                }

                if (overlay?.HeaderBar?.ActualWidth > 0 && _rootWindow.IsVisible && !PInvoke.IsIconic(new(Handle)))
                {
                    overlay.HeaderBar.UpdatePadding();
                }
            });
        }
    }

    private async void OnInputActivationChanged(InputActivationListener sender, InputActivationListenerActivationChangedEventArgs args)
    {
        // 延迟处理，避免打断正常的用户操作.
        await Task.Delay(400);
        if (IsClosed || IsDisposed)
        {
            return;
        }

        if (sender.State != InputActivationState.Deactivated)
        {
            ResetFocus();
        }

        _activeChangedAction?.Invoke(sender.State == InputActivationState.Activated);

        _presentation.RecoverOffscreenOrdinary();
    }

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (IsClosed || IsDisposed)
        {
            return;
        }

        if (args.DidSizeChange)
        {
            var size = _rootWindow.Presenter.Kind is AppWindowPresenterKind.FullScreen or AppWindowPresenterKind.CompactOverlay
                ? _rootWindow.Size
                : _rootWindow.ClientSize;
            SizeChanged?.Invoke(this, new Size(size.Width, size.Height));
        }

        if (args.DidSizeChange || args.DidVisibilityChange || args.DidPresenterChange)
        {
            UpdateXamlSourcePosition();
            _presentation.GeometryChanged();
        }

        if ((args.DidVisibilityChange || args.DidPresenterChange) && sender.IsVisible && !_presentation.IsChanging)
        {
            ResetFocus();
        }

        if (args.DidPresenterChange && sender.Presenter.Kind == AppWindowPresenterKind.Overlapped)
        {
            ResetWindowMinSize();
            UpdateXamlSourcePosition();
            DelayUpdateTitleBarPadding(true);
        }

        if (args.DidPositionChange)
        {
            _presentation.GeometryChanged();
        }
    }

    private void ResetWindowMinSize()
    {
        if (_rootWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped)
        {
            var scale = XamlRoot?.RasterizationScale ?? 1d;
            (_rootWindow.Presenter as OverlappedPresenter)?.PreferredMinimumHeight = Convert.ToInt32(WindowMinHeight * scale);
            (_rootWindow.Presenter as OverlappedPresenter)?.PreferredMinimumWidth = Convert.ToInt32(WindowMinWidth * scale);
        }
    }

    private void OnWindowDestroying(AppWindow sender, object args)
    {
        _presentation.Close();
        IsClosed = true;
        _inputActivationListener.InputActivationChanged -= OnInputActivationChanged;
        _rootWindow.Destroying -= OnWindowDestroying;
        _rootWindow.Changed -= OnWindowChanged;
        _inputActivationListener = default;

        // An OS-initiated close reaches here before the HWND is gone; the island must not
        // outlive it or the next input dispatch faults in CoreMessagingXP.
        DisposeXamlSource();
    }

    internal void UpdateXamlSourcePosition()
    {
        if (_xamlSourceDisposed)
        {
            return;
        }

        var size = _rootWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen
            ? _rootWindow.Size
            : _rootWindow.ClientSize;
        _xamlSource?.SiteBridge?.MoveAndResize(new Windows.Graphics.RectInt32(
            0,
            0,
            size.Width,
            size.Height));
    }

    internal void SetTopMost(bool isTopMost)
    {
        // 全屏和小窗模式下不处理置顶.
        if (_rootWindow.Presenter.Kind is AppWindowPresenterKind.FullScreen or AppWindowPresenterKind.CompactOverlay)
        {
            return;
        }

        PInvoke.SetWindowPos(new(Handle), isTopMost ? new(-1) : new(-2), 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
    }

    internal bool IsTopMost()
    {
        var style = PInvoke.GetWindowLong(new(Handle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        return (style & 0x00000008) != 0; // WS_EX_TOPMOST
    }

    public void ResetFocus()
    {
        DispatcherQueue.GetForCurrentThread().TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (IsClosed || IsDisposed || _xamlSourceDisposed || _xamlSource is null || _xamlSource.SiteBridge is null || _rootGrid is null)
            {
                return;
            }

            PInvoke.SetFocus(new(Handle));
            PInvoke.SetFocus(new(Win32Interop.GetWindowFromWindowId(_xamlSource.SiteBridge.WindowId)));
            if (_rootGrid.Children.Count > 0)
            {
                var element = _rootGrid.Children[0] as FrameworkElement;
                element?.Focus(FocusState.Programmatic);
            }
        });
    }
}