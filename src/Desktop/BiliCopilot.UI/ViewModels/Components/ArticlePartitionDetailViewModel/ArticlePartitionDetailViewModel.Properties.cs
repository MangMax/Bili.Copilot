// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 专栏文章分区详情视图模型.
/// </summary>
public sealed partial class ArticlePartitionDetailViewModel
{
    private readonly ILogger<ArticlePartitionDetailViewModel> _logger;
    private readonly IArticleDiscoveryService _service;
    private readonly Dictionary<string, List<ArticleItemViewModel>> _childPartitionArticleCache = new();
    private readonly Dictionary<string, int> _childPartitionOffsetCache = new();
    private readonly bool _isRecommendPartition;
    private readonly bool _isHotPartition;

    private List<ArticleItemViewModel> _recommendCache;
    private int _recommendOffset;
    private bool _preventLoadMore;

    /// <summary>
    /// 当前文章列表的代次，刷新、切换子分区或切换排序时递增.
    /// </summary>
    /// <remarks>
    /// 请求发起时记下代次，返回后若代次已变，说明列表已被替换，这批结果
    /// 不能追加、不能写回分页偏移，也不能关闭新请求的加载态。
    /// </remarks>
    private int _listGeneration;

    [ObservableProperty]
    public partial List<ArticleSortType> SortTypes { get; set; }

    [ObservableProperty]
    public partial List<PartitionViewModel> Children { get; set; }

    [ObservableProperty]
    public partial ArticleSortType SelectedSortType { get; set; }

    [ObservableProperty]
    public partial bool IsRecommend { get; set; }

    [ObservableProperty]
    public partial bool IsArticleLoading { get; set; }

    [ObservableProperty]
    public partial PartitionViewModel CurrentPartition { get; set; }

    /// <summary>
    /// 初始化完成.
    /// </summary>
    public event EventHandler Initialized;

    /// <summary>
    /// 文章列表已完成更新.
    /// </summary>
    public event EventHandler ArticleListUpdated;

    /// <summary>
    /// 显示的文章列表.
    /// </summary>
    public ObservableCollection<ArticleItemViewModel> Articles { get; } = new();
}
