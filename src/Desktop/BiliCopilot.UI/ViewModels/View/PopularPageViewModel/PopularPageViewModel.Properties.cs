// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
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

    /// <summary>
    /// 列表状态的代次，切换分区、刷新或撤销都会递增.
    /// </summary>
    /// <remarks>
    /// 刷新请求发起时记下代次，返回后若代次已变，说明请求期间用户切走了又切回来，
    /// 这批结果已经不属于当前列表，必须丢弃。
    /// </remarks>
    private int _refreshGeneration;

    /// <summary>
    /// 推荐分区刷新前的快照，非 null 表示可以撤销这次刷新.
    /// </summary>
    private RecommendRefreshSnapshot<VideoItemViewModel>? _recommendRefreshSnapshot;

    [ObservableProperty]
    private bool _isPartitionLoading;

    [ObservableProperty]
    private bool _isVideoLoading;

    [ObservableProperty]
    private IPopularSectionItemViewModel _selectedSection;

    [ObservableProperty]
    private bool _isRefreshTipVisible;

    [ObservableProperty]
    private string _refreshTipMessage = string.Empty;

    /// <summary>
    /// 区块加载完成.
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
