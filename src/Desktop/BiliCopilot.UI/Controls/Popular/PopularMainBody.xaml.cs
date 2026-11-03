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

    /// <summary>
    /// 列表为空时显示骨架，有数据就整体切换到卡片.
    /// </summary>
    /// <remarks>
    /// 骨架和卡片是两个互斥的视图状态，不做逐格替换，也不垫在卡片下面，
    /// 因此不会出现两层叠在一起，也不会因为骨架撑高 extent 而反复触发加载。
    /// </remarks>
    private void SyncSkeletons()
    {
        var budget = View.CurrentBudget > 0 ? View.CurrentBudget : InitialSkeletonEstimate;
        // 直接把条目数推给控件：x:Bind 到 Videos.Count 在集合增删时不一定能及时刷新，
        // 一旦控件读到旧值就会一直认为「还没满」，无限续页。
        View.ItemCount = ViewModel?.Videos.Count ?? 0;
        if (ViewModel is not null)
        {
            // 手动刷新和首屏共用同一套度量：刷多少由当前视口预算决定。
            ViewModel.RefreshTargetCount = View.CurrentBudget;
        }

        var isEmpty = ViewModel is null || ViewModel.Videos.Count == 0;
        var showSkeletons = _isActive && isEmpty;
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