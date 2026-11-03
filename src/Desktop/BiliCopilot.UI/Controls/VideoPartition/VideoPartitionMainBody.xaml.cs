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
    /// 骨架占位卡片；只用于撑起尚未加载的区域，不属于业务列表.
    /// </summary>
    public ObservableCollection<VideoCardSkeleton> Skeletons { get; } = new();

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
        SyncSkeletons();
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
        SyncSkeletons();
        await View.DelayCheckItemsAsync();
    }

    private void OnVideosChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncSkeletons();

    private void OnBudgetChanged(int budget) => SyncSkeletons();

    /// <summary>
    /// 列表为空时显示骨架，有数据就整体切换到卡片.
    /// </summary>
    private void SyncSkeletons()
    {
        var budget = View.CurrentBudget > 0 ? View.CurrentBudget : InitialSkeletonEstimate;
        var isEmpty = ViewModel is null || ViewModel.Videos.Count == 0;
        var showSkeletons = IsLoaded && isEmpty;
        if (showSkeletons)
        {
            while (Skeletons.Count < budget) Skeletons.Add(new VideoCardSkeleton(Skeletons.Count));
            while (Skeletons.Count > budget) Skeletons.RemoveAt(Skeletons.Count - 1);
        }
        else
        {
            Skeletons.Clear();
        }

        SkeletonRepeater.Visibility = showSkeletons ? Visibility.Visible : Visibility.Collapsed;

        // 骨架把 extent 撑成视口高度后，「滚动余量不足」会永远成立；此时只按预算判断。
        View.ExtentFallbackEnabled = !showSkeletons;
    }
}
