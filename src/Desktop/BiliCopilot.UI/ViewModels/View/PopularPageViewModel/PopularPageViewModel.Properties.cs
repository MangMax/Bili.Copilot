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

    /// <summary>
    /// 推荐流每页请求的条数.
    /// </summary>
    /// <remarks>
    /// 不传时服务端按默认页大小返回，一页过滤后只剩约 6 条，对桌面端偏少；
    /// 这里显式请求与桌面客户端相当的数量。服务端仍会过滤非视频卡片，实际返回可能少于该值。
    /// </remarks>
    private const int RecommendPageSize = 30;
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
    /// 推荐刷新是否正在进行.
    /// </summary>
    /// <remarks>
    /// 推荐刷新由左下角浮层表达进度，不走整页 LoadingWidget；这里单独防重入，
    /// 并在刷新期间挡住下滑追加，避免请求结果与追加结果互相覆盖。
    /// </remarks>
    private bool _isRecommendRefreshing;

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

    /// <summary>
    /// 刷新提示的展示状态（隐藏 / 刷新中 / 结果 / 失败）.
    /// </summary>
    [ObservableProperty]
    public partial RefreshTipState RefreshTipState { get; set; } = Toolkits.RefreshTipState.Hidden;

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
