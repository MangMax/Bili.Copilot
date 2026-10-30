// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 视频分区详情视图模型.
/// </summary>
public sealed partial class VideoPartitionDetailViewModel
{
    private readonly ILogger<VideoPartitionDetailViewModel> _logger;
    private readonly IVideoDiscoveryService _service;
    private readonly Dictionary<string, List<VideoItemViewModel>> _childPartitionVideoCache = new();
    private readonly Dictionary<string, (long Offset, int PageNumber)> _childPartitionOffsetCache = new();

    private List<VideoItemViewModel> _recommendVideoCache;
    private long _recommendOffset;

    /// <summary>
    /// 列表状态的代次，切换子分区、切换排序、刷新或撤销都会递增.
    /// </summary>
    /// <remarks>
    /// 刷新请求发起时记下代次，返回后若代次已变，说明请求期间用户切走了又切回来，
    /// 这批结果已经不属于当前列表，必须丢弃。
    /// </remarks>
    private int _refreshGeneration;

    /// <summary>
    /// 推荐子分区刷新前的快照，非 null 表示可以撤销这次刷新.
    /// </summary>
    private RecommendRefreshSnapshot<VideoItemViewModel>? _recommendRefreshSnapshot;

    [ObservableProperty]
    private List<PartitionVideoSortType> _sortTypes;

    [ObservableProperty]
    private List<PartitionViewModel> _children;

    [ObservableProperty]
    private PartitionVideoSortType _selectedSortType;

    [ObservableProperty]
    private bool _isRecommend;

    [ObservableProperty]
    private bool _isVideoLoading;

    /// <summary>
    /// 推荐刷新是否正在进行.
    /// </summary>
    /// <remarks>
    /// 推荐刷新在头部按钮旁显示进度，不走整页 LoadingWidget；这里同时兼作防重入标记，
    /// 刷新期间挡住下滑追加，避免请求结果与追加结果互相覆盖。
    /// </remarks>
    [ObservableProperty]
    private bool _isRecommendRefreshing;

    /// <summary>
    /// 是否可以撤销最近一次推荐刷新.
    /// </summary>
    /// <remarks>
    /// 由刷新快照是否存在决定。撤销入口与刷新按钮并排，只在刷新成功后出现。
    /// </remarks>
    [ObservableProperty]
    private bool _canUndoRefresh;

    [ObservableProperty]
    private PartitionViewModel _currentPartition;

    /// <summary>
    /// 初始化完成.
    /// </summary>
    public event EventHandler Initialized;

    /// <summary>
    /// 视频列表已完成更新.
    /// </summary>
    public event EventHandler VideoListUpdated;

    /// <summary>
    /// 显示的视频列表.
    /// </summary>
    public ObservableCollection<VideoItemViewModel> Videos { get; } = new();
}
