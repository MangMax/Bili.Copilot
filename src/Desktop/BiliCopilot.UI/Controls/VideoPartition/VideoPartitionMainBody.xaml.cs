// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.Components;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace BiliCopilot.UI.Controls.VideoPartition;

/// <summary>
/// 视频分区主体.
/// </summary>
public sealed partial class VideoPartitionMainBody : VideoPartitionDetailControlBase
{
    /// <summary>
    /// 布局尚未完成时先按这个数量铺骨架，等真实尺寸算出来后立刻校正.
    /// </summary>
    private const int InitialSkeletonEstimate = 16;

    private readonly RecommendationScrollAnchor _anchor;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoPartitionMainBody"/> class.
    /// </summary>
    public VideoPartitionMainBody()
    {
        InitializeComponent();
        _anchor = new(VideoRepeater, View);
        View.BudgetChanged += OnBudgetChanged;
        Loaded += (_, _) => Attach(ViewModel);
        Unloaded += (_, _) => { Detach(ViewModel); _anchor.Cancel(); };
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
    protected override void OnControlUnloaded()
    {
        Detach(ViewModel);
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(VideoPartitionDetailViewModel? oldValue, VideoPartitionDetailViewModel? newValue)
    {
        Detach(oldValue);
        if (IsLoaded) Attach(newValue);
    }

    private void Attach(VideoPartitionDetailViewModel? value)
    {
        if (value is null) return;
        Detach(value);
        value.VideoListUpdated += OnVideoListUpdatedAsync;
        value.RecommendationListChanging += _anchor.Capture;
        value.RecommendationListChanged += _anchor.Restore;
        value.PropertyChanged += OnPagePropertyChanged;
        value.Videos.CollectionChanged += OnVideosChanged;
        SyncDisplayItems();
    }

    private void Detach(VideoPartitionDetailViewModel? value)
    {
        if (value is null || _anchor is null) return;
        value.VideoListUpdated -= OnVideoListUpdatedAsync;
        value.RecommendationListChanging -= _anchor.Capture;
        value.RecommendationListChanged -= _anchor.Restore;
        value.PropertyChanged -= OnPagePropertyChanged;
        value.Videos.CollectionChanged -= OnVideosChanged;
        _anchor.Cancel();
    }

    private void OnPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewModel.CurrentPartition) or nameof(ViewModel.SelectedSortType) or nameof(ViewModel.IsRecommend)) _anchor.Cancel();
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
    private void SyncDisplayItems()
    {
        var budget = View.CurrentBudget > 0 ? View.CurrentBudget : InitialSkeletonEstimate;

        var videos = ViewModel?.Videos;
        var count = videos?.Count ?? 0;

        // 1) 真实卡片按顺序占住自己的位置（骨架随之被顶掉）。
        for (var i = 0; i < count; i++)
        {
            var item = videos![i];
            if (i < DisplayItems.Count)
            {
                if (!ReferenceEquals(DisplayItems[i], item))
                {
                    DisplayItems[i] = item;
                }
            }
            else
            {
                DisplayItems.Add(item);
            }
        }

        // 2) 列表变短时清掉多余的卡片项，骨架留到最后统一裁剪。
        for (var i = DisplayItems.Count - 1; i >= count; i--)
        {
            if (DisplayItems[i] is not VideoCardSkeleton)
            {
                DisplayItems.RemoveAt(i);
            }
        }

        // 3) 剩余位置用骨架补齐到视口预算。
        var target = Math.Max(count, budget);
        while (DisplayItems.Count < target) DisplayItems.Add(new VideoCardSkeleton(DisplayItems.Count - count));
        while (DisplayItems.Count > target) DisplayItems.RemoveAt(DisplayItems.Count - 1);

        View.ItemCount = count;
        // 还有骨架占位时只按预算判断是否续页，避免 extent 被骨架撑满后无限预取。
        View.ExtentFallbackEnabled = DisplayItems.Count <= count;
    }
}