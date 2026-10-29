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
    /// 检查或下载失败.
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
    public bool IsButtonVisible => State is AppUpdateState.Available or AppUpdateState.Downloading or AppUpdateState.Ready;

    /// <summary>
    /// 是否正在下载.
    /// </summary>
    public bool IsDownloading => State == AppUpdateState.Downloading;

    /// <summary>
    /// 进度是否不可确定（未知文件大小时使用）.
    /// </summary>
    public bool IsIndeterminate { get; private set; }

    /// <summary>
    /// 悬停提示的标题.
    /// </summary>
    public string ToolTipTitle => string.Format(
        ResourceToolkit.GetLocalizedString(StringNames.UpdateAvailableFormat),
        AvailableVersion);

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
        AppUpdateState.Failed => ResourceToolkit.GetLocalizedString(StringNames.UpdateCheckFailed),
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
    /// 打开本次更新的发布页（下载功能就绪前的占位动作）.
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
        OnPropertyChanged(nameof(StatusText));
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
