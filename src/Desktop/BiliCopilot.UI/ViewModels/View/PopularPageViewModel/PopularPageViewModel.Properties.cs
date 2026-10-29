// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 流行视频页面视图模型.
/// </summary>
public sealed partial class PopularPageViewModel
{
    private readonly IVideoDiscoveryService _service;
    private readonly ILogger<PopularPageViewModel> _logger;
    private readonly Dictionary<IPopularSectionItemViewModel, List<VideoItemViewModel>> _videoCache = new();

    private long _recommendOffset;
    private long _hotOffset;
    // 切换或刷新后，旧请求和旧调度回调不再拥有当前列表。
    private long _sectionVersion;
    // 排队切换尚未执行时，Videos 仍属于之前显示的栏目。
    private IPopularSectionItemViewModel _displayedSection;

    [ObservableProperty]
    private bool _isPartitionLoading;

    [ObservableProperty]
    private bool _isVideoLoading;

    [ObservableProperty]
    private IPopularSectionItemViewModel _selectedSection;

    /// <summary>
    /// 初始栏目选择已确定，分区目录可能仍在加载.
    /// </summary>
    public event EventHandler SectionInitialized;

    /// <summary>
    /// 视频列表已完成更新.
    /// </summary>
    public event EventHandler VideoListUpdated;

    /// <summary>
    /// 分区列表.
    /// </summary>
    public ObservableCollection<IPopularSectionItemViewModel> Sections { get; } = new();

    /// <summary>
    /// 视频列表.
    /// </summary>
    public ObservableCollection<VideoItemViewModel> Videos { get; } = new();
}
