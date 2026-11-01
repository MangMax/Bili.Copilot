// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Richasy.BiliKernel.Models;
using Richasy.Danmaku;
using Richasy.Danmaku.Enums;
using Richasy.Danmaku.Legacy;
using Richasy.Danmaku.Models;
using System.ComponentModel;

namespace BiliCopilot.UI.Controls.Danmaku;

/// <summary>
/// 视频弹幕面板.
/// </summary>
public sealed partial class VideoDanmakuPanel : DanmakuControlBase
{
    private readonly DanmakuRedrawSession _redrawSession;
    private int _lastProgress;
    private DanmakuFrostMaster? _danmakuMaster;
    private DanmakuRenderer? _danmakuRenderer;
    private DanmakuRenderViewModel? _subscribedViewModel;
    private bool _isActive;
    private bool _rendererFailed;
    private bool _rendererCleanupFailed;

    // All renderer calls and retirement run on the panel's UI dispatcher.
    // Only the published Legacy terminal protocol is handled here; validation
    // and unrelated application exceptions retain their normal diagnostics.
    private void WithRenderer(Action operation)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            var viewModel = _subscribedViewModel;
            var master = _danmakuMaster;
            var renderer = _danmakuRenderer;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (_isActive && ReferenceEquals(viewModel, _subscribedViewModel)
                    && ReferenceEquals(master, _danmakuMaster) && ReferenceEquals(renderer, _danmakuRenderer))
                {
                    WithRenderer(operation);
                }
            });
            return;
        }

        if (_rendererFailed)
        {
            return;
        }

        if (_danmakuMaster?.WorkerFailure is { } failure)
        {
            RetireFailedRenderer(failure);
            return;
        }

        try
        {
            operation();
        }
        catch (Exception error) when (_danmakuMaster?.WorkerFailure is { } cause
            && (ReferenceEquals(error, cause)
                || (error is InvalidOperationException && ReferenceEquals(error.InnerException, cause))))
        {
            RetireFailedRenderer(_danmakuMaster.WorkerFailure);
        }
    }

    private void RetireFailedRenderer(Exception failure)
    {
        _rendererFailed = true;
        _redrawSession.Invalidate();
        this.Get<ILogger<VideoDanmakuPanel>>().LogError(failure,
            "Danmaku renderer failed and was retired. Reopen playback to restore danmaku.");
        ReleaseRenderer();
    }

    public VideoDanmakuPanel()
    {
        _redrawSession = new(callback => DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => callback()));
        InitializeComponent();
        // The NuGet base unregisters its lifecycle handlers on the first unload.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        SubscribeViewModel(ViewModel);
        Redraw();
    }

    private void ReloadRenderer()
    {
        var type = ViewModel.Renderer;
        if ((type == DanmakuRendererType.DirectX && _danmakuMaster is not null)
            || (type == DanmakuRendererType.Win2D && _danmakuRenderer is not null))
        {
            ReleaseRenderer();
            if (_rendererFailed)
            {
                return;
            }
        }

        if (type == DanmakuRendererType.DirectX && _danmakuRenderer == null)
        {
            _danmakuRenderer = new DanmakuRenderer(RootGrid, this.Get<ILogger<DanmakuRenderer>>());
        }
        else if (type == DanmakuRendererType.Win2D && _danmakuMaster == null)
        {
            _danmakuMaster = new DanmakuFrostMaster(RootGrid);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _redrawSession.Invalidate();
        SubscribeViewModel(null);
        ReleaseRenderer();
    }

    private void ReleaseRenderer()
    {
        if (!_rendererFailed && _danmakuMaster?.WorkerFailure is { } failure)
        {
            RetireFailedRenderer(failure);
            return;
        }

        // Detach before cleanup: callbacks and a later unload must never reuse
        // or close the same failed instance again.
        var master = _danmakuMaster;
        var renderer = _danmakuRenderer;
        _danmakuMaster = null;
        _danmakuRenderer = null;
        try
        {
            master?.Close();
            renderer?.Dispose();
        }
        catch (Exception error) when (_rendererFailed || master?.WorkerFailure is not null)
        {
            if (!_rendererFailed)
            {
                _rendererFailed = true;
                _redrawSession.Invalidate();
                this.Get<ILogger<VideoDanmakuPanel>>().LogError(master!.WorkerFailure,
                    "Danmaku renderer failed during retirement. Reopen playback to restore danmaku.");
            }

            // A failed retirement is not permission to reuse this panel's resources.
            _rendererCleanupFailed = true;
            this.Get<ILogger<VideoDanmakuPanel>>().LogError(error, "Failed to retire the faulted danmaku renderer.");
        }
        finally
        {
            RootGrid.Children.Clear();
        }
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(DanmakuRenderViewModel? oldValue, DanmakuRenderViewModel? newValue)
    {
        _redrawSession.Invalidate();
        SubscribeViewModel(_isActive ? newValue : null);
        WithRenderer(() =>
        {
            _danmakuMaster?.Clear();
            _danmakuRenderer?.Clear();
        });
        // A different playback model is an explicit recovery boundary. Loaded,
        // visibility, style, list refresh and renderer settings are not retries.
        if (!ReferenceEquals(oldValue, newValue) && !_rendererCleanupFailed)
        {
            _rendererFailed = false;
        }

        Redraw();
    }

    private void SubscribeViewModel(DanmakuRenderViewModel? newValue)
    {
        var oldValue = _subscribedViewModel;
        if (oldValue is not null)
        {
            oldValue.RequestClearDanmaku -= OnRequestClearDanmaku;
            oldValue.ProgressChanged -= OnProgressChanged;
            oldValue.SeekRequested -= OnSeekRequested;
            oldValue.PauseDanmaku -= OnPauseDanmaku;
            oldValue.ResumeDanmaku -= OnResumeDanmaku;
            oldValue.RequestResetStyle -= OnRequestResetStyle;
            oldValue.ExtraSpeedChanged -= OnExtraSpeedChanged;
            oldValue.RequestRedrawDanmaku -= OnRedrawDanmaku;
            oldValue.RequestAddSingleDanmaku -= OnRequestAddSingleDanmaku;
            oldValue.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = newValue;
        if (newValue is null)
        {
            return;
        }

        newValue.RequestClearDanmaku += OnRequestClearDanmaku;
        newValue.ProgressChanged += OnProgressChanged;
        newValue.SeekRequested += OnSeekRequested;
        newValue.PauseDanmaku += OnPauseDanmaku;
        newValue.ResumeDanmaku += OnResumeDanmaku;
        newValue.RequestResetStyle += OnRequestResetStyle;
        newValue.ExtraSpeedChanged += OnExtraSpeedChanged;
        newValue.RequestRedrawDanmaku += OnRedrawDanmaku;
        newValue.RequestAddSingleDanmaku += OnRequestAddSingleDanmaku;
        newValue.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => WithRenderer(() => OnViewModelPropertyChangedCore(sender, e));

    private void OnViewModelPropertyChangedCore(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsShowDanmaku) && !ViewModel.IsShowDanmaku)
        {
            // Visibility is bound directly in XAML. Retire queued and active
            // rebuilds; showing again reconstructs only the latest target.
            _redrawSession.Invalidate();
            _danmakuMaster?.Stop();
            _danmakuRenderer?.Stop();
        }

        if (e.PropertyName == nameof(ViewModel.Renderer))
        {
            // Renderer replacement and redraw share one dispatcher batch.
            Redraw();
        }
    }

    private void OnRequestAddSingleDanmaku(object? sender, string e)
        => WithRenderer(() => OnRequestAddSingleDanmakuCore(sender, e));

    private void OnRequestAddSingleDanmakuCore(object? sender, string e)
    {
        if (!ViewModel.IsShowDanmaku)
        {
            return;
        }

        var location = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.DanmakuLocation, DanmakuLocation.Scroll);
        var color = AppToolkit.HexToColor(SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.DanmakuColor, Microsoft.UI.Colors.White.ToString()));
        var isStandardSize = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.IsDanmakuStandardSize, true);
        var model = new DanmakuItem
        {
            StartMs = Convert.ToUInt32(_lastProgress * 1000),
            Mode = (DanmakuMode)((int)location),
            TextColor = color,
            BaseFontSize = isStandardSize ? 20 : 24,
            Text = e,
            HasOutline = true,
            HasBorder = true
        };

        _danmakuMaster?.AddRealtimeDanmaku(model, true);
        _danmakuRenderer?.AddRealtimeDanmaku(model, true);
    }

    private void OnRequestClearDanmaku(object? sender, EventArgs e)
        => WithRenderer(() => OnRequestClearDanmakuCore(sender, e));

    private void OnRequestClearDanmakuCore(object? sender, EventArgs e)
    {
        _redrawSession.Invalidate();
        if (ViewModel.IsShowDanmaku)
        {
            _lastProgress = 0;
        }

        _danmakuMaster?.Clear();
        _danmakuRenderer?.Clear();
    }

    private void OnRequestResetStyle(object? sender, EventArgs e)
        => ResetDanmakuStyle();

    private void OnExtraSpeedChanged(object? sender, EventArgs e)
        => ResetSpeed();

    private void OnProgressChanged(object? sender, int e)
    {
        _lastProgress = e;
        UpdateDanmakuTime(e);
    }

    private void OnSeekRequested(object? sender, int position)
        => WithRenderer(() => OnSeekRequestedCore(sender, position));

    private void OnSeekRequestedCore(object? sender, int position)
    {
        if (position < 0)
        {
            return;
        }

        _lastProgress = position;
        // Keep the latest target in the view model while hidden. Showing the
        // panel redraws from that target; hidden seeks must not rebuild history.
        if (!ViewModel.IsShowDanmaku)
        {
            return;
        }

        // Apply playback intent before reconstruction, including a newly mounted renderer.
        if (ViewModel.IsPaused)
        {
            _danmakuRenderer?.Pause();
            _danmakuMaster?.Pause();
        }

        var milliseconds = Convert.ToUInt32(position * 1000d);
        _danmakuRenderer?.Seek(milliseconds);
        // UpdateTime continues Legacy's cursor after the explicit seek request.
        _danmakuMaster?.Seek(milliseconds);
        _danmakuMaster?.UpdateTime(milliseconds);
    }

    private void OnResumeDanmaku(object? sender, EventArgs e)
        => WithRenderer(() => OnResumeDanmakuCore(sender, e));

    private void OnResumeDanmakuCore(object? sender, EventArgs e)
    {
        if (!ViewModel.IsShowDanmaku)
        {
            return;
        }

        _danmakuRenderer?.Resume();
        _danmakuMaster?.Resume();
    }

    private void OnPauseDanmaku(object? sender, EventArgs e)
        => WithRenderer(() => OnPauseDanmakuCore(sender, e));

    private void OnPauseDanmakuCore(object? sender, EventArgs e)
    {
        _danmakuRenderer?.Pause();
        _danmakuMaster?.Pause();
    }

    private void OnRedrawDanmaku(object? sender, EventArgs e)
        => Redraw();

    private void ResetDanmakuStyle()
        => WithRenderer(() => ResetDanmakuStyleCore());

    private void ResetDanmakuStyleCore()
    {
        if (ViewModel is null || !ViewModel.IsShowDanmaku)
        {
            return;
        }

        if (_danmakuMaster != null)
        {
            _danmakuMaster.SetRollingDensity(-1);
            _danmakuMaster.SetOpacity(ViewModel.DanmakuOpacity);
            _danmakuMaster.SetBorderColor(Colors.Gray);
            _danmakuMaster.SetRollingAreaRatio(Convert.ToInt32(ViewModel.DanmakuArea * 10));
            _danmakuMaster.SetDanmakuFontSizeOffset(ViewModel.DanmakuFontSize);
            _danmakuMaster.SetFontFamilyName(ViewModel.DanmakuFontFamily);
            _danmakuMaster.SetIsTextBold(ViewModel.IsDanmakuBold);
            _danmakuMaster.SetDanmakuEnabledType(ViewModel.IsRollingEnabled, ViewModel.IsTopEnabled, ViewModel.IsBottomEnabled);
            _danmakuMaster.SetOutlineSize(ViewModel.OutlineSize);
        }

        if (_danmakuRenderer != null)
        {
            var config = DanmakuConfiguration.CreateDefault();
            config.TextOpacity = ViewModel.DanmakuOpacity;
            config.FontSizeScale = ViewModel.DanmakuFontSize;
            config.RollingAreaRatio = (float)ViewModel.DanmakuArea;
            config.FontFamilyName = ViewModel.DanmakuFontFamily ?? "Segoe UI";
            config.IsTextBold = ViewModel.IsDanmakuBold;
            config.OutlineSize = ViewModel.OutlineSize;

            _danmakuRenderer.SetDanmakuEnabledType(ViewModel.IsRollingEnabled, ViewModel.IsTopEnabled, ViewModel.IsBottomEnabled);
            _danmakuRenderer.UpdateConfiguration(config);
        }

        ResetSpeed();
    }

    private void ResetSpeed()
        => WithRenderer(() => ResetSpeedCore());

    private void ResetSpeedCore()
    {
        if (ViewModel is null || !ViewModel.IsShowDanmaku)
        {
            return;
        }

        var finalSpeed = ViewModel.DanmakuSpeed * 5 * ViewModel.ExtraSpeed;
        _danmakuMaster?.SetRollingSpeed(finalSpeed);
        _danmakuRenderer?.UpdateConfiguration(new DanmakuConfiguration { RollingSpeedBase = (float)finalSpeed });
    }

    private void Redraw()
    {
        if (!_isActive || !IsLoaded || ViewModel is null || !ViewModel.IsShowDanmaku)
        {
            return;
        }

        if (_rendererFailed)
        {
            return;
        }

        _redrawSession.Request(() => WithRenderer(() =>
        {
            if (!_isActive || !IsLoaded || ViewModel is null || !ViewModel.IsShowDanmaku)
            {
                return;
            }

            ReloadRenderer();
            if (_rendererFailed)
            {
                return;
            }

            var danmakus = ViewModel.GetCachedDanmakus();
            if (danmakus.Count > 0)
            {
                var list = BilibiliDanmakuParser.GetDanmakuList(danmakus, true);
                _danmakuMaster?.SetDanmakuList(list);
                _danmakuRenderer?.SetDanmakuList(list);
            }
            else
            {
                _danmakuMaster?.Clear();
                _danmakuRenderer?.Clear();
            }

            ResetDanmakuStyle();
            OnSeekRequested(ViewModel, ViewModel.Position);

            if (!ViewModel.IsPaused && ViewModel.IsShowDanmaku)
            {
                _danmakuRenderer?.Resume();
                _danmakuMaster?.Resume();
            }
            else
            {
                _danmakuRenderer?.Pause();
                _danmakuMaster?.Pause();
            }
        }));
    }

    private void UpdateDanmakuTime(double pos)
        => WithRenderer(() => UpdateDanmakuTimeCore(pos));

    private void UpdateDanmakuTimeCore(double pos)
    {
        if (pos < 0 || double.IsNaN(pos) || !ViewModel.IsShowDanmaku)
        {
            return;
        }

        _danmakuRenderer?.UpdateTime(Convert.ToUInt32(pos * 1000));
        _danmakuMaster?.UpdateTime(Convert.ToUInt32(pos * 1000));
    }
}
