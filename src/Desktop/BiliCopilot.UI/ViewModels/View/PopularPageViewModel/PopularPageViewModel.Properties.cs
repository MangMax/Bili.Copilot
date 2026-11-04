// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.Recommend;
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
    // 连续失败次数：达到上限后停止自动补页，避免失败后每次布局变化都重试、把接口打到限流。
    private int _recommendFailureCount;

    /// <summary>
    /// 连续失败多少次后暂停自动补页.
    /// </summary>
    private const int MaxRecommendFailureCount = 3;

    /// <summary>
    /// 推荐流是否已经彻底加载失败（连续失败达到上限）.
    /// </summary>
    /// <remarks>
    /// 失败后必须让骨架让位：否则页面会永远停在骨架屏上，用户既看不到内容也不知道出了什么事.
    /// 手动刷新或切换分区会把它复位.
    /// </remarks>
    [ObservableProperty]
    private bool _isRecommendLoadFailed;
    // 当前列表实际使用的推荐流来源；设置变更后要等到下一次刷新才切换，避免同一条列表混源。
    private IRecommendFeedSource? _feedSource;
    private RecommendFeedSource _feedSourceKind;

    /// <summary>
    /// 手动刷新至少取回的条数.
    /// </summary>
    private const int MinRefreshTargetCount = 6;

    /// <summary>
    /// 手动刷新最多取回的条数，避免大屏下一次刷新拉太多内容.
    /// </summary>
    private const int MaxRefreshTargetCount = 60;

    /// <summary>
    /// 单次刷新最多连续请求的页数，兜底防止来源一直返回重复内容.
    /// </summary>
    private const int MaxRefreshPageCount = 12;

    /// <summary>
    /// 首屏最多连续请求的页数.
    /// </summary>
    private const int MaxInitialPageCount = 6;
    private long _hotOffset;
    // 切换或刷新后，旧请求和旧调度回调不再拥有当前列表。
    private long _sectionVersion;
    // 排队切换尚未执行时，Videos 仍属于之前显示的栏目。
    private IPopularSectionItemViewModel _displayedSection;

    /// <summary>
    /// 列表状态的代次，切换分区、刷新或撤销都会递增.
    /// </summary>
    /// <remarks>
    /// 刷新请求发起时记下代次，返回后若代次已变，说明请求期间用户切走了又切回来，
    /// 这批结果已经不属于当前列表，必须丢弃。
    /// </remarks>
    private int _refreshGeneration;
    private int _undoSnapshotVersion;

    /// <summary>
    /// 推荐分区刷新前的快照，非 null 表示可以撤销这次刷新.
    /// </summary>
    private RecommendRefreshSnapshot<VideoItemViewModel>? _recommendRefreshSnapshot;

    /// <summary>
    /// 手动刷新希望取回的条数，由页面按当前视口预算写入.
    /// </summary>
    /// <remarks>
    /// 刷新与首屏共用同一套「一屏需要多少张」的度量：视口大就多取、视口小就少取，
    /// 与推荐来源、是否增量刷新都无关。未写入时按最小批次处理。
    /// </remarks>
    public int RefreshTargetCount { get; set; }

    [ObservableProperty]
    private bool _isPartitionLoading;

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
    [NotifyCanExecuteChangedFor(nameof(RefreshVideoCommand))]
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
    private IPopularSectionItemViewModel _selectedSection;

    /// <summary>
    /// 区块加载完成.
    /// </summary>
    public event EventHandler SectionInitialized;

    /// <summary>
    /// 视频列表已完成更新.
    /// </summary>
    public event EventHandler VideoListUpdated;

    /// <summary>
    /// 增量刷新开始，参数为本次要取回的条数.
    /// </summary>
    public event Action<int>? RecommendRefreshStarted;

    /// <summary>
    /// 增量刷新结束（成功或失败都会触发）.
    /// </summary>
    public event EventHandler? RecommendRefreshCompleted;

    public event Action<RecommendationListChange>? RecommendationListChanging;
    public event EventHandler? RecommendationListChanged;

    /// <summary>
    /// 分区列表.
    /// </summary>
    public ObservableCollection<IPopularSectionItemViewModel> Sections { get; } = new();

    /// <summary>
    /// 视频列表.
    /// </summary>
    public ObservableCollection<VideoItemViewModel> Videos { get; } = new();
}