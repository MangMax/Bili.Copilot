// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 视频分区详情视图模型.
/// </summary>
public sealed partial class VideoPartitionDetailViewModel : ViewModelBase<PartitionViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VideoPartitionDetailViewModel"/> class.
    /// </summary>
    public VideoPartitionDetailViewModel(
        PartitionViewModel partition,
        IVideoDiscoveryService service)
        : base(partition)
    {
        _service = service;
        _logger = this.Get<ILogger<VideoPartitionDetailViewModel>>();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Videos.Count > 0)
        {
            return;
        }

        if (Data.Children is not null)
        {
            var children = Data.Children.ToList();
            var rcmdPartition = new Partition(Data.Data.Id, ResourceToolkit.GetLocalizedString(StringNames.Recommend));
            children.Insert(0, new PartitionViewModel(rcmdPartition));
            Children = [.. children];
        }

        SortTypes = [.. Enum.GetValues<PartitionVideoSortType>()];
        SelectedSortType = PartitionVideoSortType.Default;
        await ChangeChildPartitionAsync(Children.First());
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ChangeSortTypeAsync(PartitionVideoSortType sortType)
    {
        if (IsRecommend)
        {
            return;
        }

        // 排序会重载列表，推荐子分区的撤销快照必须失效，避免撤销回排序前的列表。
        ClearRefreshTip();
        SelectedSortType = sortType;
        _childPartitionVideoCache.Clear();
        Videos.Clear();
        _childPartitionOffsetCache.Clear();
        await LoadVideosAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRecommend)
        {
            await RefreshRecommendVideosAsync();
            return;
        }

        // 非推荐子分区的刷新行为保持不变，但必须让推荐子分区的撤销状态失效，避免串场。
        ClearRefreshTip();
        _childPartitionVideoCache.Remove(CurrentPartition.Data.Id);
        _childPartitionOffsetCache.Remove(CurrentPartition.Data.Id);
        Videos.Clear();
        await LoadVideosAsync();
    }

    [RelayCommand]
    private void UndoRefresh()
    {
        if (_recommendRefreshSnapshot is null)
        {
            return;
        }

        var snapshot = _recommendRefreshSnapshot;

        // 撤销同样走最小差异：仍在原位且未变化的条目保持原引用，避免整列表重建。
        RecommendListDiff.Apply(Videos, snapshot.Items, p => p.Data.Identifier.Id);

        _recommendOffset = snapshot.Offset;
        ClearRefreshTip();
        VideoListUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 清除撤销快照与刷新提示.
    /// </summary>
    /// <remarks>
    /// 切换子分区、切换排序或走普通刷新时 <see cref="Videos"/> 会被替换或重载，
    /// 旧快照指向的列表已不在页面上，必须一并失效，否则撤销会把推荐列表撤销到别的子分区。
    /// </remarks>
    internal void ClearRefreshTip()
    {
        _recommendRefreshSnapshot = null;
        RefreshTipState = Toolkits.RefreshTipState.Hidden;
        _refreshGeneration++;
    }

    [RelayCommand]
    private async Task ChangeChildPartitionAsync(PartitionViewModel partition)
    {
        if (partition is null || partition.Data.Equals(CurrentPartition?.Data))
        {
            return;
        }

        ClearRefreshTip();
        IsRecommend = partition.Data.Id == Data.Data.Id;
        if (Videos.Count > 0)
        {
            if (IsRecommend)
            {
                _recommendVideoCache = Videos.ToList();
            }
            else
            {
                _childPartitionVideoCache[CurrentPartition.Data.Id] = Videos.ToList();
            }
        }

        CurrentPartition = partition;
        Videos.Clear();
        if (IsRecommend && _recommendVideoCache is not null && _recommendVideoCache.Count > 0)
        {
            foreach (var item in _recommendVideoCache)
            {
                Videos.Add(item);
            }
        }
        else if (_childPartitionVideoCache.TryGetValue(CurrentPartition.Data.Id, out var cache))
        {
            foreach (var item in cache)
            {
                Videos.Add(item);
            }
        }

        if (Videos.Count == 0)
        {
            await LoadVideosAsync();
        }
        else
        {
            VideoListUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private async Task LoadVideosAsync()
    {
        if (IsVideoLoading || _isRecommendRefreshing)
        {
            return;
        }

        IsVideoLoading = true;
        if (IsRecommend)
        {
            await LoadRecommendVideosAsync();
        }
        else
        {
            await LoadChildPartitionVideosAsync();
        }

        IsVideoLoading = false;
        VideoListUpdated?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshRecommendVideosAsync()
    {
        // 推荐刷新用左下角浮层表达进度，不再触发整页 LoadingWidget，因此单独占用一个重入标记。
        if (IsVideoLoading || _isRecommendRefreshing)
        {
            return;
        }

        var isIncremental = SettingsToolkit.ReadLocalSetting(SettingNames.IncrementalRecommendRefresh, false)
            && Videos.Count > 0;

        // 新一次刷新本身就改变了列表状态，之前发起的刷新即使还在路上也必须失效。
        _refreshGeneration++;

        // 记录发起刷新时的代次：请求期间用户可能切到别的子分区（甚至切走又切回），
        // 回来后只要代次变了，这批结果就不属于当前列表，不能写回去。
        var generation = _refreshGeneration;

        // 无论增量还是重载，都先记下刷新前的列表与偏移，成功后才有东西可撤销。
        // 列表为空时快照也是空的，撤销即回到「刷新前为空」的样子。
        var snapshot = RecommendRefreshSnapshot<VideoItemViewModel>.Capture(Videos, _recommendOffset);

        RefreshTipState = Toolkits.RefreshTipState.Refreshing(
            ResourceToolkit.GetLocalizedString(StringNames.RefreshingRecommendVideos));

        _isRecommendRefreshing = true;
        try
        {
            var (videos, offset) = await _service.GetPartitionRecommendVideoListAsync(Data.Data, 0);
            if (!RecommendRefreshResultGuard.ShouldApply(IsRecommend, generation, _refreshGeneration))
            {
                return;
            }

            var application = RecommendRefreshApplier.Apply(
                Videos,
                videos?.Select(p => new VideoItemViewModel(p, VideoCardStyle.Partition)),
                p => p.Data.Identifier.Id,
                isIncremental);

            // 只改动真正变化的条目：未变化的条目保持原引用，卡片容器不会被重建。
            RecommendListDiff.Apply(Videos, application.Items, p => p.Data.Identifier.Id);

            RefreshTipState = Toolkits.RefreshTipState.FromRefreshResult(
                isIncremental,
                application.AddedCount,
                ResourceToolkit.GetLocalizedString(StringNames.RefreshResultAdded),
                ResourceToolkit.GetLocalizedString(StringNames.RefreshResultReloaded));

            _recommendRefreshSnapshot = snapshot;
            _recommendOffset = offset;

            // 刷新后的列表才是当前推荐列表，旧的缓存不能再被切回推荐子分区时复用。
            _recommendVideoCache?.Clear();
            VideoListUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // 失败时列表、偏移与快照都保持原样，浮层收起，不展示「已刷新」。
            RefreshTipState = Toolkits.RefreshTipState.Failed;
            _logger.LogError(ex, "尝试刷新分区推荐视频时出错.");
        }
        finally
        {
            _isRecommendRefreshing = false;
        }
    }

    private async Task LoadRecommendVideosAsync()
    {
        try
        {
            var (videos, offset) = await _service.GetPartitionRecommendVideoListAsync(Data.Data, _recommendOffset);
            _recommendOffset = offset;
            TryAddVideos(videos, VideoCardStyle.Partition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载分区推荐视频时出错.");
        }
    }

    private async Task LoadChildPartitionVideosAsync()
    {
        try
        {
            var offset = 0L;
            var pn = 0;
            if (_childPartitionOffsetCache.TryGetValue(CurrentPartition.Data.Id, out var offsetCache))
            {
                offset = offsetCache.Offset;
                pn = offsetCache.PageNumber;
            }

            var (videos, nextOffset, nextPn) = await _service.GetChildPartitionVideoListAsync(CurrentPartition.Data, offset, pn, SelectedSortType);
            _childPartitionOffsetCache[CurrentPartition.Data.Id] = (nextOffset, nextPn);
            TryAddVideos(videos, VideoCardStyle.Partition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"尝试加载 {Data.Data.Name}/{CurrentPartition.Data.Name} 分区视频时出错.");
        }
    }

    private void TryAddVideos(IReadOnlyList<VideoInformation> videos, VideoCardStyle style)
    {
        if (videos is not null)
        {
            foreach (var item in videos)
            {
                Videos.Add(new VideoItemViewModel(item, style));
            }
        }
    }
}
