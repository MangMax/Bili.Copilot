// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.Toolkits;
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
        SyncDisplayItems();
    }

    private void OnRefreshCompleted(object? sender, EventArgs e)
    {
        _pendingRefreshCount = 0;
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

        // 增量刷新时顶部先占住整行位置：新卡片按页填进这些位置，
        // 后面的旧卡片就不会被一页一页往下顶。
        var columns = Math.Max(1, ScrollSlotBudget.Columns(View.ActualWidth, SlotLayout.Video));
        var pendingRows = _pendingRefreshCount > 0
            ? (int)Math.Ceiling(_pendingRefreshCount / (double)columns)
            : 0;
        var pendingSlots = pendingRows * columns;

        // 1) 顶部占位区。
        var index = 0;
        for (; index < pendingSlots; index++)
        {
            if (index < DisplayItems.Count && DisplayItems[index] is VideoCardSkeleton)
            {
                continue;
            }

            var placeholder = new VideoCardSkeleton(index);
            if (index < DisplayItems.Count) DisplayItems.Insert(index, placeholder);
            else DisplayItems.Add(placeholder);
        }

        // 2) 真实卡片按顺序占住自己的位置（占位区之后的骨架随之被顶掉）。
        for (var i = 0; i < count; i++)
        {
            var item = videos![i];
            var target = pendingSlots + i;
            if (target < DisplayItems.Count)
            {
                if (!ReferenceEquals(DisplayItems[target], item))
                {
                    DisplayItems[target] = item;
                }
            }
            else
            {
                DisplayItems.Add(item);
            }
        }

        // 3) 多余的卡片项清掉（列表变短时），骨架留到最后统一裁剪。
        var cardsEnd = pendingSlots + count;
        for (var i = DisplayItems.Count - 1; i >= cardsEnd; i--)
        {
            if (DisplayItems[i] is not VideoCardSkeleton)
            {
                DisplayItems.RemoveAt(i);
            }
        }

        // 4) 剩余位置用骨架补齐到视口预算。
        var targetCount = Math.Max(cardsEnd, budget);
        while (DisplayItems.Count < targetCount) DisplayItems.Add(new VideoCardSkeleton(DisplayItems.Count - cardsEnd));
        while (DisplayItems.Count > targetCount) DisplayItems.RemoveAt(DisplayItems.Count - 1);

        View.ItemCount = count;
        // 还有骨架占位时只按预算判断是否续页，避免 extent 被骨架撑满后无限预取。
        View.ExtentFallbackEnabled = DisplayItems.Count <= count;
    }
}