// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Richasy.BiliKernel.Models.Media;
using Serilog;
using System.Diagnostics;
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
    private AppNotificationActivatedEventArgs? _pendingNotification;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Logger.Fatal(e.ExceptionObject as Exception, "Unhandled AppDomain exception");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Log.Logger.Error(e.Exception, "Unobserved task exception");
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
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
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
            _pendingNotification ??= instance.GetActivatedEventArgs().Data as AppNotificationActivatedEventArgs;
            try
            {
                await GlobalDependencies.Accounts.InitializeAsync();
                await GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().LaunchCommand.ExecuteAsync(default);
            }
            catch (Exception ex)
            {
                this.Get<ILogger<App>>().LogError(ex, "启动账户会话失败。");
                ShowAccountStartupFailure();
            }
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

    private static void ShowAccountStartupFailure()
    {
        var window = new WinUIEx.WindowEx
        {
            Title = ResourceToolkit.GetLocalizedString(StringNames.AppName),
            Width = 520,
            Height = 180,
            Content = new TextBlock
            {
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap,
                Text = ResourceToolkit.GetLocalizedString(StringNames.LoadBiliAccountsFailed),
            },
        };
        window.Activate();
    }

    internal async Task<bool> RestartAfterUpdateAsync()
    {
        if (GetMainWindow() is not { } window)
        {
            return false;
        }

        // The SDK restart API reopens the old executable path. Activate by
        // package family after shutdown so Windows can register the staged version.
        var appId = $"{Package.Current.Id.FamilyName}!App";
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(
            $"Get-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue | Wait-Process -Timeout 60 -ErrorAction Stop; " +
            $@"& $env:windir\explorer.exe 'shell:AppsFolder\{appId}'");

        using var relauncher = Process.Start(start);
        if (relauncher is null)
        {
            return false;
        }

        if (await window.RequestExitAsync())
        {
            return true;
        }

        if (!relauncher.HasExited)
        {
            relauncher.Kill();
        }

        return false;
    }

    private static MainWindow? GetMainWindow()
        => GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Windows.Find(p => p is MainWindow) as MainWindow;

    private void OnInstanceActivated(object? sender, AppActivationArguments e)
    {
        if (e.Data is AppNotificationActivatedEventArgs notification)
        {
            QueueNotificationActivation(notification);
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => TrayLifecycle?.Restore());
        }
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
        // Activate sets the active window, but a caption-button close can leave
        // the restored window in the background. Request foreground as well.
        // Windows may still decline the request under its foreground policy.
        _ = window.SetForegroundWindow();
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
            // IconSource loads asynchronously in H.NotifyIcon 2.3.2 and can update an
            // already disposed tray after a quick disable. Own a synchronous icon instead.
            icon.Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico"));
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
        GlobalDependencies.Accounts.Current?.Get<NotificationViewModel>().Stop();
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
        => QueueNotificationActivation(args);

    private void QueueNotificationActivation(AppNotificationActivatedEventArgs args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _pendingNotification = args;
            OpenPendingNotification();
        });
    }

    internal void OpenPendingNotification()
    {
        if (_pendingNotification is not { } pending || GlobalDependencies.Kernel is null
            || TrayLifecycle?.IsExiting == true || GetMainWindow() is null)
        {
            return;
        }

        var appViewModel = this.Get<AppViewModel>();
        if (appViewModel.IsInitialLoading)
        {
            return;
        }

        _pendingNotification = null;
        var arguments = pending.Arguments;
        if (!arguments.TryGetValue("page", out var type))
        {
            return;
        }

        try
        {
            if (type == SettingsNavigationPolicy.UpdateNotificationRoute)
            {
                RestoreMainWindow();
                this.Get<NavigationViewModel>().NavigateTo(typeof(SettingsPage), SettingsSection.Update);
                return;
            }

            var accountId = this.Get<NotificationViewModel>().AccountId;
            if (accountId <= 0 || !arguments.TryGetValue("account", out var account)
                || !long.TryParse(account, out var targetAccount) || targetAccount != accountId)
            {
                return;
            }

            if (type == "moments")
            {
                RestoreMainWindow();
                this.Get<NavigationViewModel>().NavigateTo(typeof(MomentPage));
                this.Get<MomentPageViewModel>().ShowNotificationSummary(arguments.TryGetValue("section", out var section) && section == "video");
                return;
            }

            if (type == "moment")
            {
                if (arguments.TryGetValue("id", out var id) && ulong.TryParse(id, out var dynamicId) && dynamicId > 0)
                {
                    RestoreMainWindow();
                    _ = Windows.System.Launcher.LaunchUriAsync(new Uri(FormattableString.Invariant($"https://t.bilibili.com/{dynamicId}")));
                }

                return;
            }

            if (type is not ("video" or "episode"))
            {
                return;
            }

            if (arguments.TryGetValue("media", out var media) && long.TryParse(media, out var idValue) && idValue > 0)
            {
                var target = new MediaIdentifier(media, default, default);
                RestoreMainWindow();
                appViewModel.OpenPlayerCommand.Execute(type == "video"
                    ? new MediaSnapshot(new VideoInformation(target, default))
                    : new MediaSnapshot(default, new EpisodeInformation(target)));
            }
        }
        catch (Exception ex)
        {
            this.Get<ILogger<App>>().LogWarning(ex, "无法打开应用通知。");
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

    private sealed partial class TrayIconSession : ITrayIconSession
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
            var image = _icon.Icon;
            _icon.Dispose();
            image?.Dispose();
        }
    }
}
