// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls.Components;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace BiliCopilot.UI.Controls.Popular;

/// <summary>
/// 流行视频页面主体.
/// </summary>
public sealed partial class PopularMainBody : PopularPageControlBase
{
    /// <summary>
    /// 布局尚未完成时先按这个数量铺骨架，等真实尺寸算出来后立刻校正.
    /// </summary>
    private const int InitialSkeletonEstimate = 16;

    private bool _isActive;
    // 增量刷新尚未到达的条数；大于 0 时在列表顶部留出等量的骨架位置。
    private int _pendingRefreshCount;
    // 刷新开始时的列表长度，用来判断顶部占位被填掉了多少。
    private int _refreshBaselineCount;
    private readonly RecommendationScrollAnchor _anchor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PopularMainBody"/> class.
    /// </summary>
    public PopularMainBody()
    {
        InitializeComponent();
        _anchor = new(VideoRepeater, View);
        View.BudgetChanged += OnBudgetChanged;
        // The NuGet base unregisters its lifecycle handlers on the first unload.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 列表实际渲染的条目：真实卡片 + 尚未到达位置的骨架占位.
    /// </summary>
    /// <remarks>
    /// 骨架是列表里的占位项，不是另一层视图：每张卡片到达就替换掉对应位置的骨架，
    /// 用户看到的是骨架一格一格变成卡片，而不是整屏一次性切换。
    /// </remarks>
    public ObservableCollection<object> DisplayItems { get; } = new();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        ViewModel.VideoListUpdated += OnVideoListUpdatedAsync;
        ViewModel.RecommendationListChanging += _anchor.Capture;
        ViewModel.RecommendationListChanged += _anchor.Restore;
        ViewModel.RecommendRefreshStarted += OnRefreshStarted;
        ViewModel.RecommendRefreshCompleted += OnRefreshCompleted;
        ViewModel.PropertyChanged += OnPagePropertyChanged;
        ViewModel.Videos.CollectionChanged += OnVideosChanged;
        SyncDisplayItems();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        ViewModel.VideoListUpdated -= OnVideoListUpdatedAsync;
        ViewModel.RecommendationListChanging -= _anchor.Capture;
        ViewModel.RecommendationListChanged -= _anchor.Restore;
        ViewModel.RecommendRefreshStarted -= OnRefreshStarted;
        ViewModel.RecommendRefreshCompleted -= OnRefreshCompleted;
        ViewModel.PropertyChanged -= OnPagePropertyChanged;
        ViewModel.Videos.CollectionChanged -= OnVideosChanged;
        _pendingRefreshCount = 0;
        _anchor.Cancel();
    }

    private async void OnVideoListUpdatedAsync(object? sender, EventArgs e)
    {
        SyncDisplayItems();
        await View.DelayCheckItemsAsync();
    }

    private void OnPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedSection))
        {
            _anchor.Cancel();
            _pendingRefreshCount = 0;
            SyncDisplayItems();
        }
    }

    private void OnRefreshStarted(int targetCount)
    {
        _pendingRefreshCount = Math.Max(0, targetCount);
        _refreshBaselineCount = ViewModel?.Videos.Count ?? 0;
        SyncDisplayItems();
    }

    private void OnRefreshCompleted(object? sender, EventArgs e)
    {
        _pendingRefreshCount = 0;
        _refreshBaselineCount = 0;
        SyncDisplayItems();
    }

    private void OnVideosChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncDisplayItems();

    private void OnBudgetChanged(int budget) => SyncDisplayItems();

    /// <summary>
    /// 列表为空时显示骨架，有数据就整体切换到卡片.
    /// </summary>
    /// <remarks>
    /// 骨架和卡片是两个互斥的视图状态，不做逐格替换，也不垫在卡片下面，
    /// 因此不会出现两层叠在一起，也不会因为骨架撑高 extent 而反复触发加载。
    /// </remarks>
    /// <summary>
    /// 让列表里的骨架逐格被真实卡片替换.
    /// </summary>
    /// <remarks>
    /// 期望的渲染序列 = 顶部占位（增量刷新尚未到达的位置） + 真实卡片 + 尾巴占位（补齐视口预算）。
    /// 新卡片到达时天然占据顶部占位的位置，旧卡片不会被一页一页往下顶；
    /// 占位只存在于这个视图集合里，不进入业务列表、撤销快照与列差异计算。
    /// </remarks>
    private void SyncDisplayItems()
    {
        var budget = View.CurrentBudget > 0 ? View.CurrentBudget : InitialSkeletonEstimate;
        if (ViewModel is not null)
        {
            // 手动刷新和首屏共用同一套度量：刷多少由当前视口预算决定。
            ViewModel.RefreshTargetCount = View.CurrentBudget;
        }

        var videos = ViewModel?.Videos;
        var count = videos?.Count ?? 0;

        // 增量刷新：本次新插到顶部的条目数，决定还有多少顶部占位要留着。
        var inserted = Math.Max(0, count - _refreshBaselineCount);
        var leading = _pendingRefreshCount > 0
            ? Math.Max(0, _pendingRefreshCount - inserted)
            : 0;
        var tail = Math.Max(0, budget - leading - count);

        ApplyDesired(leading + count + tail, i =>
        {
            if (i < leading)
            {
                return null;
            }

            var videoIndex = i - leading;
            return videoIndex < count ? videos![videoIndex] : null;
        });

        View.ItemCount = count;
        // 还有骨架占位时只按预算判断是否续页，避免 extent 被骨架撑满后无限预取。
        View.ExtentFallbackEnabled = leading == 0 && tail == 0;
    }

    /// <summary>
    /// 把视图集合对齐到期望序列；占位项复用已有实例，避免每次同步都重建骨架.
    /// </summary>
    /// <param name="desiredCount">期望长度.</param>
    /// <param name="itemAt">按索引取真实条目，返回 null 表示该位置放骨架.</param>
    private void ApplyDesired(int desiredCount, Func<int, object?> itemAt)
    {
        for (var i = 0; i < desiredCount; i++)
        {
            var desired = itemAt(i);
            var current = i < DisplayItems.Count ? DisplayItems[i] : null;
            if (desired is null)
            {
                if (current is VideoCardSkeleton)
                {
                    continue;
                }

                desired = new VideoCardSkeleton(i);
            }
            else if (ReferenceEquals(current, desired))
            {
                continue;
            }

            if (i < DisplayItems.Count)
            {
                DisplayItems[i] = desired;
            }
            else
            {
                DisplayItems.Add(desired);
            }
        }

        while (DisplayItems.Count > desiredCount)
        {
            DisplayItems.RemoveAt(DisplayItems.Count - 1);
        }
    }
}