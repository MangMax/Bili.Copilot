// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.View;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace BiliCopilot.UI.Controls.Popular;

/// <summary>
/// 流行视频页面主体.
/// </summary>
public sealed partial class PopularMainBody : PopularPageControlBase
{
    private readonly RecommendationScrollAnchor _anchor;
    // 增量刷新尚未到达的条数；大于 0 时在列表顶部留出等量的骨架位置。
    private int _pendingRefreshCount;
    // 刷新开始时的列表长度，用来判断顶部占位被填掉了多少。
    private int _refreshBaselineCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="PopularMainBody"/> class.
    /// </summary>
    public PopularMainBody()
    {
        InitializeComponent();
        _anchor = new(VideoRepeater, View);
        View.BudgetChanged += OnBudgetChanged;
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

    /// <inheritdoc/>
    protected override void OnControlUnloaded() => Detach(ViewModel);

    /// <inheritdoc/>
    protected override void OnViewModelChanged(PopularPageViewModel? oldValue, PopularPageViewModel? newValue)
    {
        Detach(oldValue);
        if (IsLoaded)
        {
            Attach(newValue);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Attach(ViewModel);

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach(ViewModel);

    private void Attach(PopularPageViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        Detach(value);
        value.VideoListUpdated += OnVideoListUpdatedAsync;
        value.RecommendationListChanging += _anchor.Capture;
        value.RecommendationListChanged += _anchor.Restore;
        value.RecommendRefreshStarted += OnRefreshStarted;
        value.RecommendRefreshCompleted += OnRefreshCompleted;
        value.PropertyChanged += OnPagePropertyChanged;
        value.Videos.CollectionChanged += OnVideosChanged;
        SyncDisplayItems();
    }

    private void Detach(PopularPageViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        value.VideoListUpdated -= OnVideoListUpdatedAsync;
        value.RecommendationListChanging -= _anchor.Capture;
        value.RecommendationListChanged -= _anchor.Restore;
        value.RecommendRefreshStarted -= OnRefreshStarted;
        value.RecommendRefreshCompleted -= OnRefreshCompleted;
        value.PropertyChanged -= OnPagePropertyChanged;
        value.Videos.CollectionChanged -= OnVideosChanged;
        _pendingRefreshCount = 0;
        _refreshBaselineCount = 0;
        _anchor.Cancel();
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

    private async void OnVideoListUpdatedAsync(object? sender, EventArgs e)
    {
        SyncDisplayItems();
        await View.DelayCheckItemsAsync();
    }

    private void OnVideosChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncDisplayItems();

    private void OnBudgetChanged(int budget) => SyncDisplayItems();

    /// <summary>
    /// 让列表里的骨架逐格被真实卡片替换.
    /// </summary>
    /// <remarks>
    /// 期望的渲染序列 = 新卡片 + 剩余占位 + 旧卡片 + 尾巴占位（补齐视口预算）。
    /// 预留区高度恒定，旧卡片不会被一页一页往下顶；
    /// 占位只存在于这个视图集合里，不进入业务列表、撤销快照与差异计算。
    /// </remarks>
    private void SyncDisplayItems()
    {
        if (ViewModel is null)
        {
            return;
        }

        var budget = View.CurrentBudget > 0 ? View.CurrentBudget : VideoListDisplay.InitialSkeletonEstimate;
        // 手动刷新和首屏共用同一套度量：刷多少由当前视口预算决定。
        ViewModel.RefreshTargetCount = View.CurrentBudget;

        var count = ViewModel.Videos.Count;
        // 加载彻底失败时不再补尾巴骨架，页面明确呈现“没有内容”，而不是永远转圈.
        var tail = ViewModel.IsRecommendLoadFailed ? 0 : budget;
        var layout = RefreshSlotLayout.Compute(count, _refreshBaselineCount, _pendingRefreshCount, tail);

        VideoListDisplay.Apply(DisplayItems, layout.TotalCount,
            i => layout.VideoIndexAt(i) is var index and >= 0 ? ViewModel.Videos[index] : null);

        View.ItemCount = count;
        // 还有骨架占位时只按预算判断是否续页，避免 extent 被骨架撑满后无限预取。
        View.ExtentFallbackEnabled = layout.Remaining == 0 && layout.TailCount == 0;
    }
}