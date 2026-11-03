// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 下载视图模型.
/// </summary>
public sealed partial class DownloadViewModel
{
    private const string VideoUserAgent = InternalHttpExtensions.VideoUserAgent;
    private readonly ILogger<DownloadViewModel> _logger;
    private readonly IBiliTokenResolver _tokenResolver;
    private readonly IBiliCookiesResolver _cookiesResolver;
    private readonly string _bbdownPath;
    private readonly string _ffmpegPath;
    private readonly string _downloadPath;
    private readonly bool _openFolderAfterDownload;
    private string _currentUrl;
    private string _parentUrl;
    private int _currentPartIndex;

    [ObservableProperty]
    public partial List<PlayerFormatInformation> Formats { get; set; }

    [ObservableProperty]
    public partial List<VideoPart>? Parts { get; set; }

    [ObservableProperty]
    public partial List<EpisodeInformation>? Episodes { get; set; }

    [ObservableProperty]
    public partial bool HasAvailableSubtitle { get; set; }

    /// <summary>
    /// 元数据初始化完成事件.
    /// </summary>
    public event EventHandler MetaInitialized;
}
