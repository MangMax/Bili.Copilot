// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.Update;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.System;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 更新状态.
/// </summary>
internal enum AppUpdateState
{
    /// <summary>
    /// 尚未检查.
    /// </summary>
    Idle,

    /// <summary>
    /// 正在检查.
    /// </summary>
    Checking,

    /// <summary>
    /// 已是最新版本.
    /// </summary>
    UpToDate,

    /// <summary>
    /// 有新版本可下载.
    /// </summary>
    Available,

    /// <summary>
    /// 正在下载安装包.
    /// </summary>
    Downloading,

    /// <summary>
    /// 已下载完成，等待安装.
    /// </summary>
    Ready,

    /// <summary>
    /// 正在安装.
    /// </summary>
    Installing,

    /// <summary>
    /// 检查、下载或安装失败.
    /// </summary>
    Failed,
}

/// <summary>
/// 应用更新视图模型.
/// </summary>
internal sealed partial class AppUpdateViewModel : ViewModelBase
{
    private const string RepositoryUrl = "https://github.com/MangMax/Bili.Copilot";

    private readonly AppUpdateService _updateService;
    private readonly ILogger<AppUpdateViewModel> _logger;
    private bool _hasAutoChecked;
    private UpdateCheckResult? _lastResult;
    private string? _downloadedPackagePath;
    private string? _downloadedSha256;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdateViewModel"/> class.
    /// </summary>
    public AppUpdateViewModel(
        AppUpdateService updateService,
        ILogger<AppUpdateViewModel> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    /// <summary>
    /// 当前版本号.
    /// </summary>
    public string CurrentVersion => AppUpdateService.CurrentVersion;

    /// <summary>
    /// 更新入口是否应当出现.
    /// </summary>
    public bool IsButtonVisible
        => State is AppUpdateState.Available or AppUpdateState.Downloading or AppUpdateState.Ready or AppUpdateState.Installing
        || (State == AppUpdateState.Failed && _lastResult?.Status == UpdateCheckStatus.Available);

    /// <summary>
    /// 圆钮图标随状态变化.
    /// </summary>
    public FluentIcons.Common.Symbol IconSymbol
        => State is AppUpdateState.Ready or AppUpdateState.Installing or AppUpdateState.Failed
            ? FluentIcons.Common.Symbol.ArrowSync
            : FluentIcons.Common.Symbol.ArrowDownload;

    /// <summary>
    /// 是否正在下载.
    /// </summary>
    public bool IsDownloading => State == AppUpdateState.Downloading;

    /// <summary>
    /// 进度是否不可确定（未知文件大小时使用）.
    /// </summary>
    public bool IsIndeterminate => false;

    /// <summary>
    /// 悬停提示的标题.
    /// </summary>
    public string ToolTipTitle => State is AppUpdateState.Ready or AppUpdateState.Installing
        ? ResourceToolkit.GetLocalizedString(StringNames.UpdateReadyToInstall)
        : string.Format(ResourceToolkit.GetLocalizedString(StringNames.UpdateAvailableFormat), AvailableVersion);

    /// <summary>
    /// 设置页的状态文案.
    /// </summary>
    public string StatusText => State switch
    {
        AppUpdateState.Checking => ResourceToolkit.GetLocalizedString(StringNames.CheckingUpdate),
        AppUpdateState.UpToDate => ResourceToolkit.GetLocalizedString(StringNames.AlreadyUpToDate),
        AppUpdateState.Available => string.Format(
            ResourceToolkit.GetLocalizedString(StringNames.UpdateAvailableFormat),
            AvailableVersion),
        AppUpdateState.Downloading => string.Format(
            ResourceToolkit.GetLocalizedString(StringNames.UpdateDownloadingFormat),
            Math.Round(DownloadProgress)),
        AppUpdateState.Ready => ResourceToolkit.GetLocalizedString(StringNames.UpdateReadyToInstall),
        AppUpdateState.Installing => ResourceToolkit.GetLocalizedString(StringNames.UpdateInstalling),
        AppUpdateState.Failed => _lastResult?.Status == UpdateCheckStatus.NoAssetForArchitecture
            ? ResourceToolkit.GetLocalizedString(StringNames.UpdateNoPackageForArchitecture)
            : ResourceToolkit.GetLocalizedString(StringNames.UpdateCheckFailed),
        _ => string.Empty,
    };

    /// <summary>
    /// 检查更新.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task CheckAsync()
    {
        if (State == AppUpdateState.Checking)
        {
            return;
        }

        State = AppUpdateState.Checking;
        var result = await _updateService.CheckAsync(CancellationToken.None);
        _lastResult = result;

        switch (result.Status)
        {
            case UpdateCheckStatus.Available:
                AvailableVersion = result.Release!.Version;
                ReleaseNotes = result.Release.Notes;
                State = AppUpdateState.Available;
                break;
            case UpdateCheckStatus.NoAssetForArchitecture:
                AvailableVersion = result.Release!.Version;
                ReleaseNotes = result.Release.Notes;
                State = AppUpdateState.Failed;
                _logger.LogWarning("新版本没有适用于当前架构的安装包");
                break;
            case UpdateCheckStatus.UpToDate:
                State = AppUpdateState.UpToDate;
                break;
            default:
                State = AppUpdateState.Failed;
                break;
        }
    }

    /// <summary>
    /// 自动检查一次；同一进程内只触发一次.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    public async Task AutoCheckAsync()
    {
        if (_hasAutoChecked)
        {
            return;
        }

        _hasAutoChecked = true;
        await CheckAsync();
    }

    /// <summary>
    /// 主按钮的点击动作：按当前状态分发.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task PrimaryActionAsync()
    {
        switch (State)
        {
            case AppUpdateState.Available:
                await DownloadAsync();
                break;
            case AppUpdateState.Ready:
                await InstallAsync();
                break;
            case AppUpdateState.Failed:
                if (_lastResult is { Status: UpdateCheckStatus.Available, Selection.Asset: not null })
                {
                    await DownloadAsync();
                }
                else
                {
                    await CheckAsync();
                }

                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 下载安装包.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task DownloadAsync()
    {
        var selection = _lastResult?.Selection;
        if (selection?.Asset is null || State is not (AppUpdateState.Available or AppUpdateState.Failed))
        {
            return;
        }

        State = AppUpdateState.Downloading;
        DownloadProgress = 0;
        var progress = new Progress<double>(value => DownloadProgress = value);
        var path = await _updateService.DownloadAsync(selection.Asset, progress, CancellationToken.None);
        if (path is null)
        {
            State = AppUpdateState.Failed;
            return;
        }

        _downloadedPackagePath = path;
        // zip 的摘要不适用于解出来的 msix，只有裸 msix 才带期望摘要.
        _downloadedSha256 = selection.Kind == UpdateAssetKind.Msix ? selection.Asset.Sha256 : null;
        State = AppUpdateState.Ready;
    }

    /// <summary>
    /// 安装已下载的更新并重启.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task InstallAsync()
    {
        if (State != AppUpdateState.Ready || string.IsNullOrEmpty(_downloadedPackagePath))
        {
            return;
        }

        State = AppUpdateState.Installing;
        var installed = await _updateService.InstallAsync(_downloadedPackagePath, _downloadedSha256, CancellationToken.None);
        if (!installed)
        {
            State = AppUpdateState.Failed;
            return;
        }

        _logger.LogInformation("更新已安装，重启应用以完成升级");
        Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().UnregisterKey();
        Microsoft.Windows.AppLifecycle.AppInstance.Restart(default);
    }

    /// <summary>
    /// 打开本次更新的发布页.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task OpenReleaseAsync()
    {
        var version = _lastResult?.Release?.Version ?? AvailableVersion;
        var url = string.IsNullOrEmpty(version)
            ? $"{RepositoryUrl}/releases"
            : $"{RepositoryUrl}/releases/tag/v{version}";
        await Launcher.LaunchUriAsync(new Uri(url));
    }

    partial void OnStateChanged(AppUpdateState value)
    {
        OnPropertyChanged(nameof(IsButtonVisible));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IconSymbol));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ToolTipTitle));
    }

    partial void OnDownloadProgressChanged(double value)
        => OnPropertyChanged(nameof(StatusText));

    partial void OnAvailableVersionChanged(string value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ToolTipTitle));
    }

    [ObservableProperty]
    private AppUpdateState _state;

    [ObservableProperty]
    private string _availableVersion = string.Empty;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    [ObservableProperty]
    private double _downloadProgress;
}
