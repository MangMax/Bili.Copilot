// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.Recommend;
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
        // 推荐刷新在头部按钮旁表达进度，不再触发整页 LoadingWidget，因此单独占用一个重入标记。
        if (IsVideoLoading || IsRecommendRefreshing)
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
        var undoVersion = _undoSnapshotVersion;

        // 只有增量刷新才需要「刷新前列表」用于撤销；非增量是替换刷新，不提供本次撤销。
        var snapshot = isIncremental
            ? RecommendRefreshSnapshot.Capture<VideoItemViewModel>(Videos, _recommendOffset)
            : null;

        // 新一次刷新会替换旧快照，撤销入口先收起，成功后按当前模式重新决定是否出现。
        CanUndoRefresh = false;

        IsRecommendRefreshing = true;
        try
        {
            var (videos, nextOffset) = await FetchRecommendBatchAsync(isIncremental ? RecommendRefreshBatchCount : 1, generation);
            var isRecommend = SelectedSection is PopularSectionItemViewModel { Type: PopularSectionType.Recommend };
            if (!RecommendRefreshResultGuard.ShouldApply(isRecommend, generation, _refreshGeneration))
            {
                return;
            }

            if (videos.Count == 0)
            {
                KeepPreviousUndoOrInvalidate(isIncremental);
                return;
            }

            var application = RecommendRefreshApplier.Apply(
                Videos,
                videos?.Select(p => new VideoItemViewModel(p, VideoCardStyle.Recommend)),
                p => p.Data.Identifier.Id,
                isIncremental);

            // 只改动真正变化的条目：未变化的条目保持原引用，卡片容器不会被重建。
            RecommendationListChanging?.Invoke(isIncremental ? RecommendationListChange.ResetToTop : RecommendationListChange.PreserveVisibleCard);
            if (generation != _refreshGeneration) return;
            RecommendListDiff.Apply(Videos, application.Items, p => p.Data.Identifier.Id, () => generation == _refreshGeneration);
            if (generation != _refreshGeneration) return;
            RecommendationListChanged?.Invoke(this, EventArgs.Empty);
            if (generation != _refreshGeneration) return;

            // 请求期间用户可能关掉增量刷新；落地时以当前设置为准，避免入口在关闭后又亮起。
            if (isIncremental && undoVersion == _undoSnapshotVersion && SettingsToolkit.ReadLocalSetting(SettingNames.IncrementalRecommendRefresh, false))
            {
                _recommendRefreshSnapshot = snapshot;
                CanUndoRefresh = true;
            }
            else
            {
                InvalidateUndoSnapshot();
            }

            if (generation != _refreshGeneration) return;
            _recommendOffset = nextOffset;
            _videoCache.Remove(SelectedSection);

            // 替换刷新会让列表变短（例如一页约 6 条），此时也必须通知主体检查视口并继续补页，
            // 不能只在数量增长时通知，否则刷新后永远停在第一页。
            VideoListUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // A stale refresh must not publish into the current section.
            if (generation == _refreshGeneration) KeepPreviousUndoOrInvalidate(isIncremental);
            _logger.LogError(ex, "尝试刷新推荐视频时出错.");
        }
        finally
        {
            if (generation == _refreshGeneration) IsRecommendRefreshing = false;
        }
    }

    /// <summary>
    /// 刷新没有拿到新内容时，保留上一次增量刷新留下的撤销，或清掉不属于当前模式的旧快照.
    /// </summary>
    /// <param name="isIncremental">本次刷新是否为增量刷新.</param>
    private void KeepPreviousUndoOrInvalidate(bool isIncremental)
    {
        if (isIncremental && _recommendRefreshSnapshot is not null)
        {
            CanUndoRefresh = true;
        }
        else
        {
            InvalidateUndoSnapshot();
        }
    }

    /// <summary>
    /// 连续请求多页推荐并合并成一批.
    /// </summary>
    /// <returns>合并后的视频与最后一页的偏移量.</returns>
    /// <remarks>
    /// 服务端单页只返回约 6 条视频，增量刷新时一次只补 6 个卡片偏少；
    /// 这里从偏移 0 起连续取若干页，按资源 Id 去重后合并，下一页偏移以最后一页为准。
    /// 中途某一页失败时保留已取到的部分，避免整次刷新白跑。
    /// </remarks>
    private async Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> FetchRecommendBatchAsync(int batchCount, int generation)
    {
        var videos = new List<VideoInformation>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var offset = 0L;
        _feedSource = ResolveFeedSource(allowSourceChange: true);

        for (var i = 0; i < batchCount; i++)
        {
            try
            {
                var (page, nextOffset) = await _feedSource!.FetchAsync(offset);
                if (generation != _refreshGeneration || page.Count == 0) break;
                var previousCount = videos.Count;
                foreach (var video in page)
                {
                    if (seenKeys.Add(video.Identifier.Id))
                    {
                        videos.Add(video);
                    }
                }

                if (videos.Count == previousCount || nextOffset == offset) break;
                offset = nextOffset;
            }
            catch (Exception ex) when (i > 0)
            {
                // 第一页失败交给调用方按刷新失败处理；后续页失败则保留已取到的部分。
                _logger.LogError(ex, "获取推荐视频后续页时出错，保留已取到的部分.");
                break;
            }
        }

        return (videos, offset);
    }

    /// <summary>
    /// 取得当前列表应该使用的推荐流来源.
    /// </summary>
    /// <param name="allowSourceChange">是否允许在此处切换来源（刷新与首次加载允许，滚动加载不允许）.</param>
    /// <remarks>
    /// 设置改变后立即换源会让新来源的条目追加到旧来源的列表后面，造成同一条列表混源；
    /// 因此滚动加载继续用旧的来源，只有刷新或首屏才切到新来源并从头上重新取。
    /// </remarks>
    private IRecommendFeedSource ResolveFeedSource(bool allowSourceChange)
    {
        var kind = SettingsToolkit.ReadLocalSetting(SettingNames.RecommendFeedSource, RecommendFeedSource.Mobile);
        if (_feedSource is not null && _feedSourceKind == kind)
        {
            return _feedSource;
        }

        if (_feedSource is not null && !allowSourceChange)
        {
            return _feedSource;
        }

        _feedSourceKind = kind;
        _feedSource = RecommendFeedSourceFactory.Create(_service, kind);
        // 换来源后旧游标不再属于这条流，必须从头上重新取。
        _recommendOffset = 0;
        return _feedSource;
    }

    private async Task LoadRecommendVideosAsync(long version)
    {
        try
        {
            // 首屏按设置选来源；列表已有内容时沿用当前来源，避免滚动加载把新来源的条目接到旧列表后面。
            if (Videos.Count == 0 || _feedSource is null)
            {
                _feedSource = ResolveFeedSource(allowSourceChange: true);
            }

            var (videos, nextOffset) = await _feedSource.FetchAsync(_recommendOffset);
            if (version != _sectionVersion) return;
            _recommendOffset = nextOffset;
            TryAddVideos(videos, VideoCardStyle.Recommend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载推荐视频时出错.");
        }
    }

    private async Task LoadHotVideosAsync(long version)
    {
        try
        {
            var (videos, nextOffset) = await _service.GetHotVideoListAsync(_hotOffset);
            if (version != _sectionVersion)
            {
                return;
            }

            _hotOffset = nextOffset;
            TryAddVideos(videos, VideoCardStyle.Hot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载热门视频时出错.");
        }
    }

    private async Task LoadTotalRankVideosAsync(long version)
    {
        try
        {
            var videos = await _service.GetGlobalRankingListAsync();
            if (version == _sectionVersion)
            {
                TryAddVideos(videos, VideoCardStyle.Rank);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载全站排行榜视频时出错.");
        }
    }

    private async Task LoadPartitionRankVideosAsync(Partition partition, long version)
    {
        try
        {
            var videos = await _service.GetPartitionRankingListAsync(partition);
            if (version == _sectionVersion)
            {
                TryAddVideos(videos, VideoCardStyle.Rank);
            }
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
