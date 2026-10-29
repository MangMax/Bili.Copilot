// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 流行视频页面视图模型.
/// </summary>
public sealed partial class PopularPageViewModel
{
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

        // 记录发起刷新时的代次：请求期间用户可能切到别的分区（甚至切走又切回），
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
            var (videos, nextOffset) = await _service.GetRecommendVideoListAsync(0, RecommendPageSize);
            var isRecommend = SelectedSection is PopularSectionItemViewModel { Type: PopularSectionType.Recommend };
            if (!RecommendRefreshResultGuard.ShouldApply(isRecommend, generation, _refreshGeneration))
            {
                return;
            }

            var application = RecommendRefreshApplier.Apply(
                Videos,
                videos?.Select(p => new VideoItemViewModel(p, VideoCardStyle.Recommend)),
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
            _recommendOffset = nextOffset;
            _videoCache.Remove(SelectedSection);
            VideoListUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // 失败时列表与快照都保持原样，浮层收起，不展示「已刷新」。
            RefreshTipState = Toolkits.RefreshTipState.Failed;
            _logger.LogError(ex, "尝试刷新推荐视频时出错.");
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
            var (videos, nextOffset) = await _service.GetRecommendVideoListAsync(_recommendOffset, RecommendPageSize);
            _recommendOffset = nextOffset;
            TryAddVideos(videos, VideoCardStyle.Recommend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载推荐视频时出错.");
        }
    }

    private async Task LoadHotVideosAsync()
    {
        try
        {
            var (videos, nextOffset) = await _service.GetHotVideoListAsync(_hotOffset);
            _hotOffset = nextOffset;
            TryAddVideos(videos, VideoCardStyle.Hot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载热门视频时出错.");
        }
    }

    private async Task LoadTotalRankVideosAsync()
    {
        try
        {
            var videos = await _service.GetGlobalRankingListAsync();
            TryAddVideos(videos, VideoCardStyle.Rank);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载全站排行榜视频时出错.");
        }
    }

    private async Task LoadPartitionRankVideosAsync(Partition partition)
    {
        try
        {
            var videos = await _service.GetPartitionRankingListAsync(partition);
            TryAddVideos(videos, VideoCardStyle.Rank);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"尝试加载 {partition.Name} 分区排行榜视频时出错.");
        }
    }

    private async Task LoadPartitionsAsync()
    {
        IsPartitionLoading = true;

        try
        {
            var partitions = await _service.GetVideoPartitionsAsync();
            if (partitions != null)
            {
                // 去除资讯分区，因为资讯分区没有排行榜.
                foreach (var item in partitions.Where(p => p.Id != "202"))
                {
                    Sections.Add(new PopularRankPartitionViewModel(item));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载视频分区列表时出错.");
        }

        IsPartitionLoading = false;
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
