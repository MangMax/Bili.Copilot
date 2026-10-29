// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Richasy.BiliKernel.Models.Media;
using Serilog;
using System.Text.Json;
using WinUIEx;

namespace BiliCopilot.UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private const string Id = "Richasy.BiliCopilot";
    private readonly DispatcherQueue _dispatcherQueue;
    private AppNotificationManager _notificationManager;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        UnhandledException += OnUnhandledException;
        _notificationManager = AppNotificationManager.Default;
        _notificationManager.NotificationInvoked += OnAppNotificationInvoked;
        _notificationManager.Register();
    }

    /// <summary>
    /// 日志文件夹.
    /// </summary>
    public static string LoggerFolder { get; private set; }

    internal TrayLifecycle? TrayLifecycle { get; private set; }

    internal void InitializeTrayLifecycle()
    {
        TrayLifecycle ??= new TrayLifecycle(
            () => SettingsToolkit.ReadLocalSetting(SettingNames.HideWhenCloseWindow, false),
            value => SettingsToolkit.WriteLocalSetting(SettingNames.HideWhenCloseWindow, value),
            CreateTrayIcon,
            () => GetMainWindow()?.Hide(),
            RestoreMainWindow,
            ex => Log.Logger.ForContext<App>().Error(ex, "托盘生命周期操作失败"));
    }

    internal void SetTrayEnabled(bool enabled)
    {
        if (TrayLifecycle is { } lifecycle)
        {
            lifecycle.SetEnabled(enabled);
        }
        else
        {
            SettingsToolkit.WriteLocalSetting(SettingNames.HideWhenCloseWindow, enabled);
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var instance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey(Id);
        if (instance.IsCurrent)
        {
            var rootFolder = Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder;
            var fullPath = $"{rootFolder.Path}\\Logger";
            LoggerFolder = fullPath;
            if (!Directory.Exists(fullPath))
            {
                _ = Directory.CreateDirectory(fullPath);
            }

            instance.Activated += OnInstanceActivated;
            Richasy.WinUIKernel.Share.Base.ImageExBase.EnableBackgroundDecoding = false;
            GlobalDependencies.Initialize();
            GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().LaunchCommand.Execute(default);
        }
        else
        {
            var activatedArgs = instance.GetActivatedEventArgs();
            // The continuation runs on a worker thread; WinUI Exit must run on
            // this instance's UI thread or the windowless secondary stays alive.
            _ = instance.RedirectActivationToAsync(activatedArgs).AsTask()
                .ContinueWith(_ => _dispatcherQueue.TryEnqueue(() => Current.Exit()));
        }
    }

    private static MainWindow? GetMainWindow()
        => GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Windows.Find(p => p is MainWindow) as MainWindow;

    private void OnInstanceActivated(object? sender, AppActivationArguments e)
    {
        _dispatcherQueue.TryEnqueue(() => TrayLifecycle?.Restore());
    }

    private static void RestoreMainWindow()
    {
        if (GetMainWindow() is not { } window || window.Get<AppViewModel>().IsClosed)
        {
            return;
        }

        window.Show();
        if (window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        window.Activate();
    }

    private ITrayIconSession CreateTrayIcon()
    {
        // A fresh dictionary owns fresh commands and an icon on every enable.
        // Disposed TaskbarIcon resources cannot be reused.
        var resources = new ResourceDictionary { Source = new Uri("ms-appx:///Styles/TrayResources.xaml") };
        var icon = (TaskbarIcon)resources["TrayIcon"];
        var show = (XamlUICommand)resources["ShowHideWindowCommand"];
        var quit = (XamlUICommand)resources["QuitCommand"];
        show.ExecuteRequested += OnShowHideWindowCommandExecuteRequested;
        quit.ExecuteRequested += OnQuitCommandExecuteRequested;
        var session = new TrayIconSession(icon, () =>
        {
            show.ExecuteRequested -= OnShowHideWindowCommandExecuteRequested;
            quit.ExecuteRequested -= OnQuitCommandExecuteRequested;
        });
        try
        {
            icon.ForceCreate(enablesEfficiencyMode: false);
            if (!icon.IsCreated)
            {
                throw new InvalidOperationException("The notification area did not create the tray icon.");
            }

            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    internal void ReleaseTrayResources()
    {
        TrayLifecycle?.Dispose();
        if (_notificationManager is not null)
        {
            _notificationManager.NotificationInvoked -= OnAppNotificationInvoked;
        }

        _notificationManager?.Unregister();
        _notificationManager = null;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        this.Get<ILogger<App>>().LogError(e.Exception, "Unhandled exception occurred.");
        e.Handled = true;

        // 一旦出现布局循环检测异常，就删除上次选中的功能页，然后重启应用.
        if (e.Message.Contains("Layout cycle detected"))
        {
            SettingsToolkit.DeleteLocalSetting(Models.Constants.SettingNames.LastSelectedFeaturePage);
            this.Get<AppViewModel>().RestartCommand.Execute(default);
        }
    }

    private void OnAppNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments.TryGetValue("page", out var type))
        {
            var argsStr = args.Arguments.TryGetValue("args", out var a) ? a : string.Empty;
            if (string.IsNullOrEmpty(argsStr))
            {
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (TrayLifecycle?.IsExiting == true)
                {
                    return;
                }

                var identifier = JsonSerializer.Deserialize(argsStr, GlobalSerializeContext.Default.MediaIdentifier);
                if (type == "video")
                {
                    this.Get<AppViewModel>().OpenPlayerCommand.Execute(new MediaSnapshot(new VideoInformation(identifier, default)));
                }
                else if (type == "episode")
                {
                    this.Get<AppViewModel>().OpenPlayerCommand.Execute(new MediaSnapshot(default, new EpisodeInformation(identifier)));
                }
            });
        }
    }

    private async void OnQuitCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        if (GetMainWindow() is { } window)
        {
            await window.RequestExitAsync();
        }
    }

    private void OnShowHideWindowCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        => TrayLifecycle?.Restore();

    private sealed class TrayIconSession : ITrayIconSession
    {
        private readonly TaskbarIcon _icon;
        private readonly Action _unsubscribe;
        private bool _disposed;

        public TrayIconSession(TaskbarIcon icon, Action unsubscribe)
        {
            _icon = icon;
            _unsubscribe = unsubscribe;
            // H.NotifyIcon 2.3.2 subscribes its synchronous recreation handler in
            // TaskbarIcon's constructor. Subscribe to that SAME message window after
            // it: IsCreated now reflects the completed attempt, not stale pre-Shell
            // state or the temporary false state during removal. No cross-HWND race.
            _icon.TrayIcon.MessageWindow.TaskbarCreated += OnTaskbarCreated;
        }

        public event EventHandler? HealthChanged;

        public bool IsCreated => !_disposed && _icon.IsCreated;

        private void OnTaskbarCreated(object? sender, EventArgs e)
        {
            if (!_disposed)
            {
                HealthChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _icon.TrayIcon.MessageWindow.TaskbarCreated -= OnTaskbarCreated;
            HealthChanged = null;
            _unsubscribe();
            _icon.Dispose();
        }
    }
}
