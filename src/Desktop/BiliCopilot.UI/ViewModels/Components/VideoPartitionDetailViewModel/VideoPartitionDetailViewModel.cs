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
        ClearRefreshSnapshot();
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
        ClearRefreshSnapshot();
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
        ++_refreshGeneration;
        IsVideoLoading = false;

        // 撤销同样走最小差异：仍在原位且未变化的条目保持原引用，避免整列表重建。
        RecommendationListChanging?.Invoke(true);
        RecommendListDiff.Apply(Videos, snapshot.Items, p => p.Data.Identifier.Id);

        for (var i = 0; i < snapshot.Items.Count; i++)
        {
            if (!ReferenceEquals(Videos[i], snapshot.Items[i])) Videos[i] = snapshot.Items[i];
        }

        RecommendationListChanged?.Invoke(this, EventArgs.Empty);
        _recommendOffset = snapshot.Offset;
        ClearRefreshSnapshot();
        VideoListUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 清除撤销快照.
    /// </summary>
    /// <remarks>
    /// 切换子分区、切换排序或走普通刷新时 <see cref="Videos"/> 会被替换或重载，
    /// 旧快照指向的列表已不在页面上，必须一并失效，否则撤销会把推荐列表撤销到别的子分区。
    /// </remarks>
    internal void ClearRefreshSnapshot()
    {
        _recommendRefreshSnapshot = null;
        CanUndoRefresh = false;
        _refreshGeneration++;
        IsRecommendRefreshing = false;
        IsVideoLoading = false;
    }

    [RelayCommand]
    private async Task ChangeChildPartitionAsync(PartitionViewModel partition)
    {
        if (partition is null || partition.Data.Equals(CurrentPartition?.Data))
        {
            return;
        }

        ClearRefreshSnapshot();
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

        IsRecommend = partition.Data.Id == Data.Data.Id;
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
        if (IsVideoLoading || IsRecommendRefreshing)
        {
            return;
        }

        IsVideoLoading = true;
        var generation = _refreshGeneration;
        var previousCount = Videos.Count;
        if (IsRecommend)
        {
            await LoadRecommendVideosAsync(generation);
        }
        else
        {
            await LoadChildPartitionVideosAsync(generation);
        }

        if (generation != _refreshGeneration) return;
        IsVideoLoading = false;
        if (Videos.Count > previousCount) VideoListUpdated?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshRecommendVideosAsync()
    {
        // 推荐刷新在头部按钮旁表达进度，不再触发整页 LoadingWidget，因此单独占用一个重入标记。
        if (IsVideoLoading || IsRecommendRefreshing)
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

        // 新一次刷新会替换旧快照，撤销入口先收起，成功后重新出现。
        CanUndoRefresh = false;

        IsRecommendRefreshing = true;
        try
        {
            var (videos, offset) = await _service.GetPartitionRecommendVideoListAsync(Data.Data, 0);
            if (!RecommendRefreshResultGuard.ShouldApply(IsRecommend, generation, _refreshGeneration))
            {
                return;
            }

            if (videos is null || videos.Count == 0)
            {
                CanUndoRefresh = _recommendRefreshSnapshot is not null;
                return;
            }

            var previousCount = Videos.Count;
            var application = RecommendRefreshApplier.Apply(
                Videos,
                videos?.Select(p => new VideoItemViewModel(p, VideoCardStyle.Partition)),
                p => p.Data.Identifier.Id,
                isIncremental);

            // 只改动真正变化的条目：未变化的条目保持原引用，卡片容器不会被重建。
            RecommendationListChanging?.Invoke(false);
            RecommendListDiff.Apply(Videos, application.Items, p => p.Data.Identifier.Id);
            RecommendationListChanged?.Invoke(this, EventArgs.Empty);

            _recommendRefreshSnapshot = snapshot;
            CanUndoRefresh = true;
            _recommendOffset = offset;

            // 刷新后的列表才是当前推荐列表，旧的缓存不能再被切回推荐子分区时复用。
            _recommendVideoCache?.Clear();
            if (Videos.Count > previousCount) VideoListUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // A stale refresh must not publish into the current section.
            if (generation == _refreshGeneration) CanUndoRefresh = _recommendRefreshSnapshot is not null;
            _logger.LogError(ex, "尝试刷新分区推荐视频时出错.");
        }
        finally
        {
            if (generation == _refreshGeneration) IsRecommendRefreshing = false;
        }
    }

    private async Task LoadRecommendVideosAsync(int generation)
    {
        try
        {
            var (videos, offset) = await _service.GetPartitionRecommendVideoListAsync(Data.Data, _recommendOffset);
            if (generation != _refreshGeneration) return;
            _recommendOffset = offset;
            TryAddVideos(videos, VideoCardStyle.Partition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载分区推荐视频时出错.");
        }
    }

    private async Task LoadChildPartitionVideosAsync(int generation)
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
            if (generation != _refreshGeneration) return;
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
