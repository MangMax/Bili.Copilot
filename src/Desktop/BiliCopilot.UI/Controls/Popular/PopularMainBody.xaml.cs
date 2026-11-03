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
    /// 骨架占位卡片；只用于撑起尚未加载的区域，不属于业务列表.
    /// </summary>
    public ObservableCollection<VideoCardSkeleton> Skeletons { get; } = new();

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
        ViewModel.PropertyChanged += OnPagePropertyChanged;
        ViewModel.Videos.CollectionChanged += OnVideosChanged;
        SyncSkeletons();
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
        ViewModel.PropertyChanged -= OnPagePropertyChanged;
        ViewModel.Videos.CollectionChanged -= OnVideosChanged;
        _anchor.Cancel();
    }

    private async void OnVideoListUpdatedAsync(object? sender, EventArgs e)
    {
        SyncSkeletons();
        await View.DelayCheckItemsAsync();
    }

    private void OnPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedSection))
        {
            _anchor.Cancel();
            SyncSkeletons();
        }
    }

    private void OnVideosChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncSkeletons();

    private void OnBudgetChanged(int budget) => SyncSkeletons();

    private void SyncSkeletons()
    {
        var budget = View.CurrentBudget > 0 ? View.CurrentBudget : InitialSkeletonEstimate;
        // 手动刷新和首屏共用同一套度量：刷多少由当前视口预算决定。
        if (ViewModel is not null)
        {
            ViewModel.RefreshTargetCount = View.CurrentBudget;
        }

        var plan = VideoSkeletonPlanner.Resolve(budget, ViewModel?.Videos.Count ?? budget);
        while (Skeletons.Count < plan.SlotCount) Skeletons.Add(new VideoCardSkeleton(Skeletons.Count));
        while (Skeletons.Count > plan.SlotCount) Skeletons.RemoveAt(Skeletons.Count - 1);

        var showSkeletons = _isActive && plan.Visible && ViewModel is not null;
        SkeletonRepeater.Visibility = showSkeletons ? Visibility.Visible : Visibility.Collapsed;

        // 骨架把 extent 撑成视口高度后，「滚动余量不足」会永远成立；
        // 此时必须关掉这条兜底，否则会无限预取。
        View.ExtentFallbackEnabled = !showSkeletons;
    }
}
