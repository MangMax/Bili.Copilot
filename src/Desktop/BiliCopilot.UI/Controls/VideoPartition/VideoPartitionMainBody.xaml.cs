// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace BiliCopilot.UI.Controls.VideoPartition;

/// <summary>
/// 视频分区主体.
/// </summary>
public sealed partial class VideoPartitionMainBody : VideoPartitionDetailControlBase
{
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
        var budget = View.CurrentBudget > 0 ? View.CurrentBudget : VideoListDisplay.InitialSkeletonEstimate;
        var videos = ViewModel?.Videos;
        var count = videos?.Count ?? 0;
        var desiredCount = Math.Max(count, budget);
        VideoListDisplay.Apply(DisplayItems, desiredCount, i => i < count ? videos![i] : null);

        View.ItemCount = count;
        View.ExtentFallbackEnabled = desiredCount <= count;
    }
}