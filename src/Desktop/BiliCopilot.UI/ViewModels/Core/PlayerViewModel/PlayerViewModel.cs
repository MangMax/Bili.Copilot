// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls.Core;
using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Player;
using BiliCopilot.UI.Resolvers;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Richasy.WinUIKernel.Share.ViewModels;
using System.Diagnostics.CodeAnalysis;
using WinUIEx;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放器视图模型.
/// </summary>
[SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", Justification = "原生资源由 ReleaseSessionAsync 通过播放会话的释放闸门只释放一次。")]
public sealed partial class PlayerViewModel(DispatcherQueue queue, ILogger<PlayerViewModel> logger) : ViewModelBase, IAsyncDisposable
{
    private PlaybackSession<PlayerViewModel>? _session;
    private InputPointerSource? _pointerSource;
    private InputKeyboardSource? _keyboardSource;
    private bool _isApplyingSubtitlePreference;
    private string? _rememberedSubtitleId;

    /// <summary>
    /// Gets 由播放会话工厂创建并登记的生命周期归属；关闭、初始化与释放都经过它。
    /// </summary>
    private PlaybackSession<PlayerViewModel> Session
        => _session ?? throw new InvalidOperationException("播放会话尚未创建");

    /// <summary>
    /// 绑定由工厂创建的播放会话；一个视图模型只属于一个会话。
    /// </summary>
    /// <param name="session">播放会话。</param>
    internal void AttachSession(PlaybackSession<PlayerViewModel> session)
        => _session = session ?? throw new ArgumentNullException(nameof(session));

    /// <summary>
    /// 原生关闭、返回和应用退出共享同一个任务，完成后才从播放器集合移除。
    /// </summary>
    public Task CloseAsync() => Session.CloseAsync(PrepareCloseAsync);

    private async Task PrepareCloseAsync()
    {
        // Reject and cancel old danmaku work before native destruction or async cleanup.
        InvalidateForRelease();
        await CompleteCloseAsync();
    }

    private async Task CompleteCloseAsync()
    {
        var appVM = this.Get<AppViewModel>();
        await ((App)Application.Current).TrayLifecycle!.ClosePlayerAsync(
            SettingsToolkit.ReadLocalSetting(SettingNames.HideMainWindowOnPlay, true),
            UseIntegrationOperation,
            appVM.Players.Count,
            appVM.IsMainWindowVisible(),
            ShowMainWindow,
            async () =>
            {
                if (Window?.IsClosed == false)
                {
                    Window.Close();
                }

                await DisposeAsync();
            },
            () => appVM.Windows.OfType<MainWindow>().FirstOrDefault()?.Close());
    }

    private async Task TryCleanupAsync(Func<Task> cleanup)
    {
        try
        {
            await cleanup();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清理播放器时失败");
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // 立即失效（幂等），再走会话的释放闸门：重复调用加入同一个任务，原生资源只释放一次。
        InvalidateForRelease();
        return new ValueTask(Session.ReleaseAsync());
    }

    /// <summary>
    /// 释放会话已创建的原生资源；只由会话的释放闸门调用一次。
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    internal Task ReleaseSessionAsync() => ReleaseSessionCoreAsync(DisposeResourcesAsync);

    private async Task DisposeResourcesAsync()
    {
        try
        {
            await ReleasePlaybackEventsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "释放播放器事件时失败");
        }

        if (Player is not null)
        {
            Player.PropertyChanged -= OnPlayerPropertyChanged;
            Player.PropertyChanged -= OnDanmakuPlayerPropertyChanged;
            // MpvPlayer.DisposeAsync saves history and then disposes its client.
            // Do not destroy that client before the history save has completed.
            await TryCleanupAsync(() => Player.DisposeAsync().AsTask());
        }

        if (Client?.IsDisposed == false)
        {
            await TryCleanupAsync(() => Client.DisposeAsync().AsTask());
        }

        if (Window?.IsDisposed == false)
        {
            Window.SizeChanged -= OnWindowSizeChanged;
            Window.GetWindow().Destroying -= OnWindowDestroying;
            await TryCleanupAsync(() => Window.DisposeAsync().AsTask());
        }
    }

    private async Task ReleasePlaybackEventsAsync()
    {
        // 关闭前把仍在去抖中的音量落盘，避免最后一次调整丢失.
        FlushPendingVolume();
        Danmaku.ClearAll();
        if (_sponsorBlockLoadSource is not null)
        {
            await _sponsorBlockLoadSource.CancelAsync();
            _sponsorBlockLoadSource.Dispose();
            _sponsorBlockLoadSource = null;
        }

        if (_rightKeyLongPressTimer != null)
        {
            _rightKeyLongPressTimer.Tick -= OnRightKeyLongPressTimerTick;
            _rightKeyLongPressTimer.Stop();
            _rightKeyLongPressTimer = null;
        }

        if (_tripleTimer != null)
        {
            _tripleTimer.Tick -= OnTripleTimerTick;
            _tripleTimer.Stop();
            _tripleTimer = null;
        }

        if (_smtc != null)
        {
            _smtc.ButtonPressed -= OnSmtcButtonPressed;
            _smtc = null;
        }

        _nativeMp?.Dispose();
        _nativeMp = null;

        if (_pointerSource is not null)
        {
            _pointerSource.PointerReleased -= OnWindowPointerReleased;
            _pointerSource = null;
        }

        if (_keyboardSource is not null)
        {
            _keyboardSource.KeyDown -= OnWindowKeyDown;
            _keyboardSource.KeyUp -= OnWindowKeyUp;
            _keyboardSource = null;
        }

        if (Connector != null)
        {
            Connector.PlaylistInitialized -= OnConnectorPlaylistInitialized;
            Connector.NewMediaRequest -= OnConnectorNewMediaRequest;
            Connector.RequestOpenExtraPanel -= OnRequestOpenExtraPanel;
            Connector.PropertiesUpdated -= OnConnectorPropertiesUpdated;
        }

        if (Client is not null)
        {
            Client.ErrorOccurred -= OnClientErrorOccurred;
            Client.CacheStateChanged -= OnClientCacheStateChanged;
        }
    }

    [RelayCommand]
    internal Task InitializeAsync(MediaSnapshot snapshot)
    {
        if (this.Get<AppViewModel>().IsClosed)
        {
            return Task.CompletedTask;
        }

        return ObserveInitializationAsync(Session.InitializeAsync(() => InitializeCoreAsync(snapshot)));

        async Task ObserveInitializationAsync(Task initialization)
        {
            try
            {
#pragma warning disable VSTHRD003 // 初始化任务由本会话创建，等待它不会阻塞 UI 线程。
                await initialization;
#pragma warning restore VSTHRD003
            }
            catch (Exception ex)
            {
                // 初始化失败会回滚会话；这里保留原有的失败记录。
                logger.LogError(ex, "初始化播放器失败");
                throw;
            }
        }
    }

    private async Task InitializeCoreAsync(MediaSnapshot snapshot)
    {
        CancelPlaybackRefresh();
        _isPlaybackRefreshReload = false;
        _danmakuPlaybackVersion++;
        _danmakuLoadSession?.Cancel();
        _snapshot = snapshot;
        IsLive = LiveModePolicy.IsLive(snapshot.Type);
        ErrorMessage = default;
        ErrorActionText = default;
        HasErrorAction = false;
        IsPreviewPlayback = false;
        CacheSpeedText = default;
        ShowPlayerWindow();
        IsConnecting = true;
        IsSourceSelectable = false;
        IsDanmakuEnabled = false;
        IsDanmakuControlVisible = false;
        IsDanmakuLoading = false;
        IsStatsOverlayShown = false;
        IsEnd = false;
        _isTlsFailed = false;
        _prevPosition = 0;
        ResetSponsorBlock();
        IsSubtitleEmpty = true;
        ApplySubtitleEnabledPreference(SettingsToolkit.ReadLocalSetting(SettingNames.IsSubtitleEnabled, true), persist: false);
        _isDanmakuInitialized = false;
        _isChapterInitialized = false;
        IsChapterListAvailable = false;
        IsNextTipShownInThisMedia = false;
        Danmaku.ClearAll();
        Chapters.Clear();
        IsChapterVisible = SettingsToolkit.ReadLocalSetting(SettingNames.IsChapterVisible, true);
        UseIntegrationOperation = SettingsToolkit.ReadLocalSetting(SettingNames.HideMainWindowOnPlay, true)
            && SettingsToolkit.ReadLocalSetting(SettingNames.UseIntegrationWhenSinglePlayWindow, true);
        BackwardTip = string.Format(ResourceToolkit.GetLocalizedString(StringNames.BackwardTemplate), SettingsToolkit.ReadLocalSetting(SettingNames.StepBackwardSecond, 10d));
        ForwardTip = string.Format(ResourceToolkit.GetLocalizedString(StringNames.ForwardTemplate), SettingsToolkit.ReadLocalSetting(SettingNames.StepForwardSecond, 30d));
        RequestHideNextTip?.Invoke(this, EventArgs.Empty);
        if (Client != null)
        {
            // Finish old-media speed writes before loading another media on this client.
            await WaitForSpeedChangesAsync();
            await WaitForPositionChangesAsync();
            try
            {
                await Client.PauseAsync();
                await Client.StopAsync();
            }
            catch
            {
            }
        }

        await InitializeInternalAsync();
        if (Session.IsClosing || Session.IsReleased)
        {
            return;
        }

        if (Connector != null)
        {
            System.Diagnostics.Debug.WriteLine($"Player initialized with connector: {Connector.GetType().Name}");
            await Connector.InitializeAsync(_snapshot, default);
            if (Window.IsClosed)
            {
                return;
            }
        }

        if (!IsLive)
        {
            StartSponsorBlockLoading();
        }

        Title = Connector switch
        {
            VideoConnectorViewModel videoVM => videoVM.Title,
            PgcConnectorViewModel pgcVM => pgcVM.Title,
            LiveConnectorViewModel liveVM => liveVM.Title,
            _ => string.Empty
        };

        if (IsLive && Window?.GetWindow() is { } liveWindow)
        {
            // 直播标题同时驱动自绘标题栏与系统窗口标题。
            liveWindow.Title = Title ?? string.Empty;
        }

        // TODO: 创建源处理器
        switch (_snapshot.Type)
        {
            case BiliMediaType.Video:
                {
                    var resolver = this.Get<VideoMediaSourceResolver>();
                    var connector = Connector as VideoConnectorViewModel;
                    resolver.Initialize(_snapshot, connector._part);
                    _sourceResolver = resolver;
                }
                break;
            case BiliMediaType.Pgc:
                {
                    var resolver = this.Get<PgcMediaSourceResolver>();
                    var connector = Connector as PgcConnectorViewModel;
                    resolver.Initialize(_snapshot, connector._episode);
                    _sourceResolver = resolver;

                    // 先在界面上拿到判定结果：播放器内核会把取源异常吞成一条日志，
                    // 只有在这里拦住才能把"需要大会员/仅可试看"的原因交给用户。
                    if (!await resolver.TryPrepareAsync())
                    {
                        ShowPlaybackUnavailable(resolver.UnavailableReason);
                        IsConnecting = false;
                        RefreshPlaybackCommand.NotifyCanExecuteChanged();
                        CheckBlackBackgroundVisible();
                        return;
                    }

                    IsPreviewPlayback = resolver.IsPreviewPlayback;
                }
                break;
            case BiliMediaType.Live:
                {
                    var resolver = this.Get<LiveMediaSourceResolver>();
                    resolver.Initialize(_snapshot);
                    _sourceResolver = resolver;
                }

                break;
            default:
                break;
        }

        OnPropertyChanged(nameof(IsPlaybackRefreshAvailable));
        RefreshPlaybackCommand.NotifyCanExecuteChanged();
        _sourceResolver.WindowHandle = Window!.Handle;

        if (_historyResolver != null && Player != null)
        {
            await _historyResolver.SaveHistoryAsync(Player.Position, Player.Duration, Richasy.MpvKernel.Core.Enums.MpvPlayerState.End, true);
        }

        // TODO: 创建历史记录处理器
        switch (_snapshot.Type)
        {
            case BiliMediaType.Video:
                {
                    var resolver = this.Get<VideoMediaHistoryResolver>();
                    var connector = Connector as VideoConnectorViewModel;
                    resolver.Initialize(connector._view, connector._part, _snapshot);
                    _historyResolver = resolver;
                }
                break;
            case BiliMediaType.Pgc:
                {
                    var resolver = this.Get<PgcMediaHistoryResolver>();
                    var connector = Connector as PgcConnectorViewModel;
                    resolver.Initialize(connector._view, connector._episode, _snapshot);
                    _historyResolver = resolver;
                }
                break;
            case BiliMediaType.Live:
                {
                    // 直播不写历史也不续播：用空实现满足播放器的调用契约。
                    _historyResolver = this.Get<LiveMediaHistoryResolver>();
                }

                break;
            default:
                break;
        }

        if (Window.IsClosed)
        {
            return;
        }

        if (Player is null)
        {
            Player = new Richasy.MpvKernel.Player.MpvPlayer(Client!, _sourceResolver, _historyResolver, logger: logger);
            Player.PropertyChanged += OnPlayerPropertyChanged;
            Player.PropertyChanged += OnDanmakuPlayerPropertyChanged;
        }
        else
        {
            Player.UpdateResolvers(_sourceResolver, _historyResolver);
        }

        try
        {
            await Player.InitializeAsync();
        }
        catch (Exception ex)
        {
            if (IsLive)
            {
                ShowLivePlaybackFailure(LiveFailureClassifier.Classify(ex));
            }
            else
            {
                ErrorMessage = ex.Message;
            }

            return;
        }

        if (Window.IsClosed)
        {
            return;
        }

        await ResetPlayerPresenterModeAsync();
        if (Window.IsClosed)
        {
            return;
        }

        CheckFullScreen();
        CheckCompactOverlay();
        ProgressChanged?.Invoke(this, 0);
        await LoadSourcesAsync();
        await LoadSubtitlesAsync();
        if (Window.IsClosed)
        {
            return;
        }

        IsConnecting = false;
        RefreshPlaybackCommand.NotifyCanExecuteChanged();
    }

    public void UpdateTheme(ElementTheme theme)
        => Window?.SetTheme(theme);

    /// <summary>
    /// 关闭播放器.
    /// </summary>
    public void Close() => _ = ObserveCloseAsync();

    private async Task ObserveCloseAsync()
    {
        try
        {
            await CloseAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "关闭播放器时失败");
        }
    }

    private void ShowPlayerWindow()
    {
        if (Window?.IsDisposed != false)
        {
            Window = new PlayerWindow();
            Window.SetActiveAction(isActive => queue.TryEnqueue(DispatcherQueuePriority.Low, () => IsWindowDeactivated = !isActive));
            Window.GetWindow().Destroying += OnWindowDestroying;
            Window.SizeChanged += OnWindowSizeChanged;
            _pointerSource = InputPointerSource.GetForIsland(Window.XamlRoot?.ContentIsland);
            _pointerSource.PointerReleased += OnWindowPointerReleased;
            _keyboardSource = InputKeyboardSource.GetForIsland(Window.XamlRoot?.ContentIsland);
            _keyboardSource.KeyDown += OnWindowKeyDown;
            _keyboardSource.KeyUp += OnWindowKeyUp;
        }

        var hideMainWindowOnPlay = SettingsToolkit.ReadLocalSetting(SettingNames.HideMainWindowOnPlay, true);

        if (!Window.GetWindow().IsVisible)
        {
            Window!.Show(Player == null && !hideMainWindowOnPlay);
        }

        var isFirstLoad = !_uiElementSetted;
        if (!_uiElementSetted)
        {
            var overlay = new PlayerOverlay { ViewModel = this };
            Window!.SetUIElement(overlay);
            _uiElementSetted = true;

            var currentTheme = SettingsToolkit.ReadLocalSetting(SettingNames.AppTheme, ElementTheme.Default);
            Window.SetTheme(currentTheme);
        }

        if (hideMainWindowOnPlay && isFirstLoad)
        {
            var mainWnd = this.Get<Core.AppViewModel>().Windows.First(p => p is MainWindow);
            if (mainWnd.AppWindow.IsVisible)
            {
                var scaleFactor = HwndExtensions.GetDpiForWindow(mainWnd.GetWindowHandle()) / 96d;
                var windowLeft = SettingsToolkit.ReadLocalSetting(SettingNames.MainWindowPositionLeft, 0);
                var windowTop = SettingsToolkit.ReadLocalSetting(SettingNames.MainWindowPositionTop, 0);
                var windowWidth = SettingsToolkit.ReadLocalSetting(SettingNames.MainWindowWidth, 1120d);
                var windowHeight = SettingsToolkit.ReadLocalSetting(SettingNames.MainWindowHeight, 740d);
                Window.GetWindow().MoveAndResize(new Windows.Graphics.RectInt32(windowLeft, windowTop, Convert.ToInt32(windowWidth * scaleFactor), Convert.ToInt32(windowHeight * scaleFactor)));
                if (SettingsToolkit.ReadLocalSetting(SettingNames.IsMainWindowMaximized, false))
                {
                    (Window.GetWindow().Presenter as OverlappedPresenter)?.Maximize();
                }

                mainWnd.Hide();
            }
        }

        if (isFirstLoad)
        {
            var defaultPresenter = SettingsToolkit.ReadLocalSetting(SettingNames.PlayerPresenterMode, PlayerPresenterMode.Default);
            if (defaultPresenter == PlayerPresenterMode.FullScreen)
            {
                Window.EnterFullScreen();
            }
            else if (defaultPresenter == PlayerPresenterMode.CompactOverlay)
            {
                Window.EnterCompactOverlay();
            }
        }

        IsVerticalScreen = Window!.GetWindow().Presenter.Kind is Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen or Microsoft.UI.Windowing.AppWindowPresenterKind.CompactOverlay
            ? Window.GetWindow().Size.Height > Window.GetWindow().Size.Width
            : Window.GetWindow().Size.Height >= Window.GetWindow().Size.Width;
        CheckBlackBackgroundVisible();
    }

    private async Task SelectSourceAsync(SourceItemViewModel source)
    {
        if (source is null || !await PreparePlaybackIntentAsync())
        {
            return;
        }

        foreach (var item in Sources)
        {
            item.IsSelected = item.Equals(source);
        }

        var selected = Sources.FirstOrDefault(s => s.IsSelected);
        SelectedSource = selected;
        // 选择可能被关闭或换集追上：先捕获会话，重播前复查。
        var isCurrent = CapturePlaybackValidity();
        _continuePosition = Player?.Position;
        if (_sourceResolver is VideoMediaSourceResolver videoResolver)
        {
            _snapshot.PreferQuality = selected.Data.Quality;
            videoResolver.ResetSnapshot(_snapshot);
            await ReplayCurrentPositionAsync(isCurrent);
        }
        else if (_sourceResolver is PgcMediaSourceResolver pgcResolver)
        {
            _snapshot.PreferQuality = selected.Data.Quality;
            pgcResolver.ResetSnapshot(_snapshot);
            await ReplayCurrentPositionAsync(isCurrent);
        }

        await Task.CompletedTask;
    }

    private void CheckBottomProgressBarVisible()
        => IsBottomProgressBarVisible = !IsLive && !IsControlsVisible && SettingsToolkit.ReadLocalSetting(SettingNames.IsBottomProgressBarVisible, true);

    partial void OnIsLiveChanged(bool value)
    {
        OnPropertyChanged(nameof(IsProgressBarVisible));
        OnPropertyChanged(nameof(IsSeekAvailable));
    }

    partial void OnIsControlsVisibleChanged(bool value)
        => Window?.SetCaptionButtonVisibility(value);

    partial void OnIsControlsVisibleChanged(bool oldValue, bool newValue)
    {
        IsTopMost = Window?.IsTopMost() ?? false;
        CheckBottomProgressBarVisible();
    }

    partial void OnIsSubtitleEnabledChanged(bool value)
    {
        if (_isApplyingSubtitlePreference)
        {
            // 恢复设置、关闭态选轨等内部修改只更新状态，副作用由调用方负责。
            return;
        }

        // 用户手动切换：先写盘，保证播放器窗口被直接关闭也不会丢失偏好；再作用于播放器。
        ApplySubtitleEnabledPreference(value, persist: true);
        ToggleSubtitleEnabledCommand.Execute(value);
    }
}
