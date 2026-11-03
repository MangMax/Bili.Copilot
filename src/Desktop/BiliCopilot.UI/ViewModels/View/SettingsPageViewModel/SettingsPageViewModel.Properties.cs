// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using Richasy.MpvKernel;
using Richasy.MpvKernel.Core.Enums;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 设置页面视图模型.
/// </summary>
public sealed partial class SettingsPageViewModel
{
    private bool _isInitialized;
    private string? _initialCustomLibMpvPath;

    [ObservableProperty]
    public partial bool IsRestartTipShown { get; set; }

    [ObservableProperty]
    public partial ElementTheme AppTheme { get; set; }

    [ObservableProperty]
    public partial string AppThemeText { get; set; }

    [ObservableProperty]
    public partial bool IsTopNavShown { get; set; }

    [ObservableProperty]
    public partial bool IsAutoPlayNextRecommendVideo { get; set; }

    [ObservableProperty]
    public partial PlayerDisplayMode DefaultPlayerDisplayMode { get; set; }

    [ObservableProperty]
    public partial bool PlayNextWithoutTip { get; set; }

    [ObservableProperty]
    public partial bool EndWithPlaylist { get; set; }

    [ObservableProperty]
    public partial bool IsMpvCustomOptionVisible { get; set; }

    [ObservableProperty]
    public partial PreferCodecType PreferCodec { get; set; }

    [ObservableProperty]
    public partial PreferQualityType PreferQuality { get; set; }

    [ObservableProperty]
    public partial PreferDecodeType PreferDecode { get; set; }

    [ObservableProperty]
    public partial MTCBehavior MTCBehavior { get; set; }

    [ObservableProperty]
    public partial bool PlayerSpeedEnhancement { get; set; }

    [ObservableProperty]
    public partial bool GlobalPlayerSpeed { get; set; }

    [ObservableProperty]
    public partial string PackageVersion { get; set; }

    [ObservableProperty]
    public partial string Copyright { get; set; }

    [ObservableProperty]
    public partial bool IsCopyScreenshot { get; set; }

    [ObservableProperty]
    public partial bool IsOpenScreenshotFile { get; set; }

    [ObservableProperty]
    public partial bool HideWhenCloseWindow { get; set; }

    [ObservableProperty]
    public partial bool IsNotificationEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsVideoMomentNotificationEnabled { get; set; }

    [ObservableProperty]
    public partial bool BottomProgressVisible { get; set; }

    [ObservableProperty]
    public partial bool NoP2P { get; set; }

    /// <summary>
    /// 是否启用空降助手.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSponsorBlockEnabled { get; set; } = true;

    /// <summary>
    /// 是否在进度条上绘制片段标记.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSponsorBlockMarkerVisible { get; set; }

    [ObservableProperty]
    public partial string DefaultDownloadPath { get; set; }

    [ObservableProperty]
    public partial bool DownloadWithDanmaku { get; set; }

    [ObservableProperty]
    public partial bool OpenFolderAfterDownload { get; set; }

    [ObservableProperty]
    public partial bool UseExternalBBDown { get; set; }

    [ObservableProperty]
    public partial bool OnlyCopyCommandWhenDownload { get; set; }

    [ObservableProperty]
    public partial bool WithoutCredentialWhenGenDownloadCommand { get; set; }

    [ObservableProperty]
    public partial bool FilterAISubtitle { get; set; }

    [ObservableProperty]
    public partial bool IsAIStreamingResponse { get; set; }

    [ObservableProperty]
    public partial bool IsWebDavEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWebDavEmpty { get; set; }

    [ObservableProperty]
    public partial List<PlayerDisplayMode> PlayerDisplayModeCollection { get; set; }

    [ObservableProperty]
    public partial List<PreferCodecType> PreferCodecCollection { get; set; }

    [ObservableProperty]
    public partial List<PreferQualityType> PreferQualityCollection { get; set; }

    [ObservableProperty]
    public partial List<PreferDecodeType> PreferDecodeCollection { get; set; }

    [ObservableProperty]
    public partial List<ExternalPlayerType> ExternalPlayerTypeCollection { get; set; }

    [ObservableProperty]
    public partial List<MTCBehavior> MTCBehaviorCollection { get; set; }

    [ObservableProperty]
    public partial bool UseWebPlayerWhenLive { get; set; }

    [ObservableProperty]
    public partial bool ShowSearchRecommend { get; set; }

    [ObservableProperty]
    public partial bool IsPopularNavVisible { get; set; }

    [ObservableProperty]
    public partial bool IsMomentNavVisible { get; set; }

    [ObservableProperty]
    public partial bool IsVideoNavVisible { get; set; }

    [ObservableProperty]
    public partial bool IsLiveNavVisible { get; set; }

    [ObservableProperty]
    public partial bool IsAnimeNavVisible { get; set; }

    [ObservableProperty]
    public partial bool IsCinemaNavVisible { get; set; }

    [ObservableProperty]
    public partial bool IsArticleNavVisible { get; set; }

    [ObservableProperty]
    public partial ScreenshotAction ScreenshotAction { get; set; }

    [ObservableProperty]
    public partial string? ScreenshotFolderPath { get; set; }

    [ObservableProperty]
    public partial double TempPlaybackRate { get; set; }

    [ObservableProperty]
    public partial bool ScrollAccelerate { get; set; }

    [ObservableProperty]
    public partial bool IncrementalRecommendRefresh { get; set; }

    [ObservableProperty]
    public partial bool CacheOnDisk { get; set; }

    [ObservableProperty]
    public partial string? CacheDirDescription { get; set; }

    [ObservableProperty]
    public partial bool HasCustomCacheDir { get; set; }

    [ObservableProperty]
    public partial bool AudioExclusiveEnabled { get; set; }

    [ObservableProperty]
    public partial string? CustomLibMpvPath { get; set; }

    [ObservableProperty]
    public partial bool CanResetLibMpvPath { get; set; }

    [ObservableProperty]
    public partial bool IsMpvExtraSettingExpanded { get; set; }

    [ObservableProperty]
    public partial double MaxVolume { get; set; }

    [ObservableProperty]
    public partial HrSeekType HrSeek { get; set; }

    [ObservableProperty]
    public partial double MaxCacheSize { get; set; }

    [ObservableProperty]
    public partial double MaxBackCacheSize { get; set; }

    [ObservableProperty]
    public partial double MaxCacheSeconds { get; set; }

    [ObservableProperty]
    public partial bool UseIntegrationWhenSinglePlayWindow { get; set; }

    [ObservableProperty]
    public partial MpvBuiltInProfile MpvBuiltInProfile { get; set; }

    [ObservableProperty]
    public partial string TotalImageCacheSize { get; set; }

    [ObservableProperty]
    public partial AudioChannelLayoutType PreferAudioChannelLayout { get; set; }

    [ObservableProperty]
    public partial bool IsTotalImageCacheClearEnabled { get; set; }

    [ObservableProperty]
    public partial double StepForwardSecond { get; set; }

    [ObservableProperty]
    public partial double StepBackwardSecond { get; set; }

    [ObservableProperty]
    public partial MpvLogLevel LogLevel { get; set; }

    [ObservableProperty]
    public partial bool HideMainWindowOnPlay { get; set; }

    [ObservableProperty]
    public partial string D3D11AdapterName { get; set; }

    [ObservableProperty]
    public partial string VulkanDeviceName { get; set; }

    /// <summary>
    /// GPU设备列表.
    /// </summary>
    public ObservableCollection<string> GpuDevices { get; } = [];
}
