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
public enum AppUpdateState
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
    /// 更新已登记，等待重启完成注册.
    /// </summary>
    RestartRequired,

    /// <summary>
    /// 检查、下载或安装失败.
    /// </summary>
    Failed,
}

/// <summary>
/// 应用更新视图模型.
/// </summary>
public sealed partial class AppUpdateViewModel : ViewModelBase
{
    private readonly AppUpdateService _updateService;
    private readonly UpdateDownloader _downloader;
    private readonly UpdateInstaller _installer;
    private readonly ILogger<AppUpdateViewModel> _logger;
    private bool _hasAutoChecked;
    private UpdateCheckResult? _lastResult;
    private UpdateDownloadResult? _downloaded;
    private UpdateAsset? _downloadAsset;
    private string? _failureDetail;
    private string _failureTitle = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdateViewModel"/> class.
    /// </summary>
    public AppUpdateViewModel(
        AppUpdateService updateService,
        UpdateDownloader downloader,
        UpdateInstaller installer,
        ILogger<AppUpdateViewModel> logger)
    {
        _updateService = updateService;
        _downloader = downloader;
        _installer = installer;
        _logger = logger;
        if (AppUpdateService.SupportsUpdates
            && SettingsToolkit.ReadLocalSetting(SettingNames.UpdateInstalledVersion, string.Empty) == AppUpdateService.CurrentVersion)
        {
            InstalledReleaseNotes = SettingsToolkit.ReadLocalSetting(SettingNames.UpdateInstalledNotes, string.Empty);
        }
    }

    /// <summary>
    /// 上次通过应用更新安装的当前版本说明.
    /// </summary>
    public string InstalledReleaseNotes { get; } = string.Empty;

    public bool HasInstalledReleaseNotes => !string.IsNullOrWhiteSpace(InstalledReleaseNotes);

    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    public bool CanCheck => AppUpdateService.SupportsUpdates
        && State is not (AppUpdateState.Checking or AppUpdateState.Downloading or AppUpdateState.Ready
            or AppUpdateState.Installing or AppUpdateState.RestartRequired)
        && _downloaded is null;

    public bool CanRunPrimaryAction => State is AppUpdateState.Available or AppUpdateState.Ready or AppUpdateState.RestartRequired
        || (State == AppUpdateState.Failed && (CanRetryDownload || HasNoPackageForArchitecture));

    public bool CanOpenRelease => _lastResult?.Release is not null
        && !HasNoPackageForArchitecture && IsButtonVisible;

    /// <summary>
    /// 更新入口是否应当出现.
    /// </summary>
    public bool IsButtonVisible => State is AppUpdateState.Available
        or AppUpdateState.Downloading
        or AppUpdateState.Ready
        or AppUpdateState.Installing
        or AppUpdateState.RestartRequired
        || (State == AppUpdateState.Failed && (CanRetryDownload || HasNoPackageForArchitecture));

    /// <summary>
    /// 圆钮图标随状态变化：就绪与安装中换成重启语义.
    /// </summary>
    public FluentIcons.Common.Symbol IconSymbol
        => State switch
        {
            AppUpdateState.Ready or AppUpdateState.Installing or AppUpdateState.RestartRequired => FluentIcons.Common.Symbol.ArrowRotateClockwise,
            AppUpdateState.Failed => FluentIcons.Common.Symbol.ArrowSync,
            _ => FluentIcons.Common.Symbol.ArrowDownload,
        };

    /// <summary>
    /// 新版本没有适用于本架构的安装包：入口仍出现，但点击只会打开发布页.
    /// </summary>
    public bool HasNoPackageForArchitecture
        => State == AppUpdateState.Failed && _lastResult?.Status == UpdateCheckStatus.NoAssetForArchitecture;

    /// <summary>
    /// 是否正在下载.
    /// </summary>
    public bool IsDownloading => State == AppUpdateState.Downloading;

    /// <summary>
    /// 悬停提示的标题.
    /// </summary>
    public string ToolTipTitle => StatusText;

    /// <summary>
    /// 当前更新操作的无障碍名称.
    /// </summary>
    public string ActionName => State is AppUpdateState.Downloading or AppUpdateState.Installing
        ? StatusText
        : ResourceToolkit.GetLocalizedString(State switch
    {
        AppUpdateState.Available => StringNames.UpdateDownloadAction,
        AppUpdateState.Ready => StringNames.UpdateInstallAction,
        AppUpdateState.RestartRequired => StringNames.UpdateRestartAction,
        AppUpdateState.Failed when HasNoPackageForArchitecture => StringNames.UpdateOpenReleaseAction,
        AppUpdateState.Failed when _downloaded is not null => StringNames.UpdateRetryInstallAction,
        AppUpdateState.Failed => StringNames.UpdateRetryDownloadAction,
        _ => StringNames.CheckUpdate,
    });

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
        AppUpdateState.Downloading when DownloadProgress >= 100 => ResourceToolkit.GetLocalizedString(StringNames.UpdateVerifying),
        AppUpdateState.Downloading when IsIndeterminate => ResourceToolkit.GetLocalizedString(StringNames.UpdateDownloadingUnknown),
        AppUpdateState.Downloading => string.Format(
            ResourceToolkit.GetLocalizedString(StringNames.UpdateDownloadingFormat),
            Math.Round(DownloadProgress)),
        AppUpdateState.Ready => ResourceToolkit.GetLocalizedString(StringNames.UpdateReadyToInstall),
        AppUpdateState.Installing => ResourceToolkit.GetLocalizedString(StringNames.UpdateInstalling),
        AppUpdateState.RestartRequired => string.IsNullOrEmpty(_failureDetail)
            ? ResourceToolkit.GetLocalizedString(StringNames.UpdateRestartRequired)
            : ResourceToolkit.GetLocalizedString(StringNames.UpdateRestartFailed),
        AppUpdateState.Failed => BuildFailureText(),
        _ => AppUpdateService.SupportsUpdates ? string.Empty : ResourceToolkit.GetLocalizedString(StringNames.UpdateUnsupportedPackage),
    };

    /// <summary>
    /// 已拿到安装包、但下载失败时可以原地重试.
    /// </summary>
    private bool CanRetryDownload
        => _lastResult is { Status: UpdateCheckStatus.Available, Selection.Asset: not null };

    /// <summary>
    /// 检查更新.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        if (!CanCheck)
        {
            return;
        }

        State = AppUpdateState.Checking;
        _failureDetail = null;
        AvailableVersion = string.Empty;
        ReleaseNotes = string.Empty;
        _failureTitle = ResourceToolkit.GetLocalizedString(StringNames.UpdateCheckFailed);
        var result = await _updateService.CheckAsync(CancellationToken.None);
        _lastResult = result;
        _downloadAsset = result.Selection.Asset;

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
                break;
            case UpdateCheckStatus.UpToDate:
                State = AppUpdateState.UpToDate;
                break;
            default:
                _failureDetail = result.Error;
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
    [RelayCommand(CanExecute = nameof(CanRunPrimaryAction))]
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
            case AppUpdateState.Failed when HasNoPackageForArchitecture:
                await OpenReleaseAsync();
                break;
            case AppUpdateState.Failed when _downloaded is not null:
                await InstallAsync();
                break;
            case AppUpdateState.Failed when CanRetryDownload:
                await DownloadAsync();
                break;
            case AppUpdateState.RestartRequired:
                await RestartApplicationAsync();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 打开本次更新的发布页.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task OpenReleaseAsync()
    {
        var tag = _lastResult?.Release?.Tag;
        var url = string.IsNullOrEmpty(tag)
            ? $"{UpdateConstants.RepositoryUrl}/releases"
            : $"{UpdateConstants.RepositoryUrl}/releases/tag/{tag}";
        await Launcher.LaunchUriAsync(new Uri(url));
    }

    private async Task DownloadAsync()
    {
        if (_downloadAsset is not { } asset)
        {
            return;
        }

        DownloadProgress = -1;
        State = AppUpdateState.Downloading;
        _failureDetail = null;

        _failureTitle = ResourceToolkit.GetLocalizedString(StringNames.UpdateDownloadFailed);
        _downloaded = null;
        var progress = new Progress<double>(value =>
        {
            if (State == AppUpdateState.Downloading)
            {
                DownloadProgress = value;
            }
        });
        var result = await _downloader.DownloadAsync(asset, progress, CancellationToken.None);
        if (result.PackagePath is null)
        {
            _failureDetail = result.Error;
            State = AppUpdateState.Failed;
            return;
        }

        _downloaded = result;
        State = AppUpdateState.Ready;
    }

    private async Task InstallAsync()
    {
        if (_downloaded is null)
        {
            return;
        }

        _failureDetail = null;
        _failureTitle = ResourceToolkit.GetLocalizedString(StringNames.UpdateInstallFailed);
        State = AppUpdateState.Installing;
        var installed = await _installer.InstallAsync(
            _downloaded,
            AvailableVersion,
            CancellationToken.None);
        if (!installed.Succeeded)
        {
            _failureDetail = installed.Error;
            if (installed.DependencyResolutionFailed && _downloadAsset?.IsMsix == true && _lastResult?.Archive is { } archive)
            {
                _downloadAsset = archive;
                _downloaded = null;
                _failureDetail = ResourceToolkit.GetLocalizedString(StringNames.UpdateDownloadWithRuntime);
            }
            else if (installed.RequiresNewDownload)
            {
                _downloaded = null;
            }

            State = AppUpdateState.Failed;
            return;
        }

        try
        {
            SettingsToolkit.WriteLocalSetting(SettingNames.UpdateInstalledNotes, ReleaseNotes);
            SettingsToolkit.WriteLocalSetting(SettingNames.UpdateInstalledVersion, AvailableVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法保存更新说明");
        }

        State = AppUpdateState.RestartRequired;
        _logger.LogInformation("更新已安装，重启应用以完成升级");
        await RestartApplicationAsync();
    }

    private string BuildFailureText()
        => _lastResult?.Status == UpdateCheckStatus.NoAssetForArchitecture
            ? ResourceToolkit.GetLocalizedString(StringNames.UpdateNoPackageForArchitecture)
            : string.IsNullOrWhiteSpace(_failureDetail)
                ? _failureTitle
                : string.Format(
                    ResourceToolkit.GetLocalizedString(StringNames.UpdateOperationFailedFormat),
                    _failureTitle,
                    _failureDetail);

    private async Task RestartApplicationAsync()
    {
        try
        {
            if (App.Current is App app && await app.RestartAfterUpdateAsync())
            {
                return;
            }

            _logger.LogWarning("更新已登记，但未能启动重启流程");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新已登记，但自动重启未完成");
        }

        _failureDetail = ResourceToolkit.GetLocalizedString(StringNames.UpdateRestartFailed);
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ToolTipTitle));
    }

    partial void OnStateChanged(AppUpdateState value)
    {
        CheckCommand.NotifyCanExecuteChanged();
        PrimaryActionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRunPrimaryAction));
        OnPropertyChanged(nameof(CanOpenRelease));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(ActionName));
        OnPropertyChanged(nameof(IsButtonVisible));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IconSymbol));
        OnPropertyChanged(nameof(HasNoPackageForArchitecture));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ToolTipTitle));
    }

    partial void OnDownloadProgressChanged(double value)
    {
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(ActionName));
        OnPropertyChanged(nameof(ToolTipTitle));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnAvailableVersionChanged(string value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ToolTipTitle));
    }

    [ObservableProperty]
    public partial AppUpdateState State { get; set; }

    [ObservableProperty]
    public partial string AvailableVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReleaseNotes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    /// <summary>
    /// 下载总大小未知时进度不可确定.
    /// </summary>
    public bool IsIndeterminate => DownloadProgress < 0;
}
