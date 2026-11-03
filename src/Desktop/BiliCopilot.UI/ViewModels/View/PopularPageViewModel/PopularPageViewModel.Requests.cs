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

        // 换来源必须整表替换：增量刷新会保留旧来源的条目，列表就混源了，撤销也会退回混源状态。
        var configuredKind = SettingsToolkit.ReadLocalSetting(SettingNames.RecommendFeedSource, RecommendFeedSource.Mobile);
        var isSourceSwitch = _feedSource is not null && _feedSourceKind != configuredKind;
        var isIncremental = !isSourceSwitch
            && SettingsToolkit.ReadLocalSetting(SettingNames.IncrementalRecommendRefresh, false)
            && Videos.Count > 0;

        // 新一次刷新本身就改变了列表状态，之前发起的刷新即使还在路上也必须失效。
        _refreshGeneration++;

        // 记录发起刷新时的代次：请求期间用户可能切到别的分区（甚至切走又切回），
        // 回来后只要代次变了，这批结果就不属于当前列表，不能写回去。
        var generation = _refreshGeneration;
        var undoVersion = _undoSnapshotVersion;
        var previousOffset = _recommendOffset;

        // 只有增量刷新才需要「刷新前列表」用于撤销；非增量是替换刷新，不提供本次撤销。
        var snapshot = isIncremental
            ? RecommendRefreshSnapshot.Capture<VideoItemViewModel>(Videos, _recommendOffset)
            : null;

        // 替换刷新会先清空列表，失败时用它把旧内容还回去。
        var backup = isIncremental ? null : Videos.ToList();

        // 新一次刷新会替换旧快照，撤销入口先收起，成功后按当前模式重新决定是否出现。
        CanUndoRefresh = false;

        IsRecommendRefreshing = true;
        var target = ResolveRefreshTargetCount();
        var applied = 0;
        var listReset = false;
        var isRecommend = SelectedSection is PopularSectionItemViewModel { Type: PopularSectionType.Recommend };
        // 点击即进入加载形态：视图先按目标条数在顶部预留骨架位置，数据一页一页落地。
        RecommendRefreshStarted?.Invoke(target);
        try
        {

            var nextOffset = await FetchRecommendPagesAsync(target, generation, page =>
            {
                if (generation != _refreshGeneration)
                {
                    return;
                }

                if (!listReset)
                {
                    // 第一页到了才动列表：刷新失败时内容和视口都保持原样。
                    listReset = true;
                    RecommendationListChanging?.Invoke(RecommendationListChange.ResetToTop);
                    if (!isIncremental)
                    {
                        Videos.Clear();
                    }
                }

                applied += ApplyRefreshPage(page, isIncremental);
                RecommendRefreshProgress?.Invoke(Math.Max(0, target - applied));
                VideoListUpdated?.Invoke(this, EventArgs.Empty);
            });

            if (listReset)
            {
                // 整批落地后再恢复视口：中途逐页恢复会让列表在用户脚下反复跳动。
                RecommendationListChanged?.Invoke(this, EventArgs.Empty);
            }

            if (!RecommendRefreshResultGuard.ShouldApply(isRecommend, generation, _refreshGeneration))
            {
                return;
            }

            if (applied == 0)
            {
                // 一条都没拿到：替换刷新把旧列表和游标一起还回去，增量刷新原本就没动列表。
                RestoreRefreshBackup(backup);
                _recommendOffset = previousOffset;
                KeepPreviousUndoOrInvalidate(isIncremental);
                return;
            }

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
        }
        catch (Exception ex)
        {
            // A stale refresh must not publish into the current section.
            if (generation == _refreshGeneration)
            {
                if (applied == 0)
                {
                    RestoreRefreshBackup(backup);
                    _recommendOffset = previousOffset;
                }

                KeepPreviousUndoOrInvalidate(isIncremental);
            }

            _logger.LogError(ex, "尝试刷新推荐视频时出错.");
        }
        finally
        {
            if (generation == _refreshGeneration) IsRecommendRefreshing = false;
            RecommendRefreshCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 把替换刷新清掉的旧列表还回去.
    /// </summary>
    /// <param name="backup">刷新前的列表，增量刷新时为 null.</param>
    private void RestoreRefreshBackup(List<VideoItemViewModel>? backup)
    {
        if (backup is null || Videos.Count > 0)
        {
            return;
        }

        foreach (var item in backup)
        {
            Videos.Add(item);
        }
    }

    /// <summary>
    /// 把一页推荐落到列表里.
    /// </summary>
    /// <param name="page">本页视频，已按资源 Id 去重.</param>
    /// <param name="isIncremental">增量刷新把新条目插到顶部，替换刷新直接追加.</param>
    /// <returns>真正加入列表的条数.</returns>
    private int ApplyRefreshPage(IReadOnlyList<VideoInformation> page, bool isIncremental)
    {
        if (!isIncremental)
        {
            foreach (var video in page)
            {
                Videos.Add(new VideoItemViewModel(video, VideoCardStyle.Recommend));
            }

            return page.Count;
        }

        var existing = new HashSet<string>(Videos.Select(p => p.Data.Identifier.Id), StringComparer.Ordinal);
        var added = 0;
        // 倒序插入才能保持本页的原始顺序。
        for (var i = page.Count - 1; i >= 0; i--)
        {
            var video = page[i];
            if (!existing.Add(video.Identifier.Id))
            {
                continue;
            }

            Videos.Insert(0, new VideoItemViewModel(video, VideoCardStyle.Recommend));
            added++;
        }

        return added;
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
    /// <summary>
    /// 计算本次刷新要取回多少条：跟随视口预算，并夹在 6~60 之间.
    /// </summary>
    /// <returns>目标条数.</returns>
    private int ResolveRefreshTargetCount()
        => Math.Clamp(
            RefreshTargetCount > 0 ? RefreshTargetCount : MinRefreshTargetCount,
            MinRefreshTargetCount,
            MaxRefreshTargetCount);

    /// <summary>
    /// 逐页取推荐，每取到一页就立刻交给调用方落地.
    /// </summary>
    /// <param name="targetCount">目标条数.</param>
    /// <param name="generation">发起刷新时的代次.</param>
    /// <param name="onPage">每页的回调，参数已按资源 Id 去重.</param>
    /// <returns>最后一页的游标.</returns>
    private async Task<long> FetchRecommendPagesAsync(
        int targetCount,
        int generation,
        Action<IReadOnlyList<VideoInformation>> onPage)
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var offset = 0L;
        var applied = 0;
        _feedSource = ResolveFeedSource(allowSourceChange: true);

        // 按「条数」而不是「页数」取：手机端一页约 6 条、网页端一批可达 20 条，
        // 同样的视口预算在不同来源下需要的请求次数并不相同。
        for (var i = 0; i < MaxRefreshPageCount && applied < targetCount; i++)
        {
            try
            {
                var (page, nextOffset) = await _feedSource!.FetchAsync(offset);
                if (generation != _refreshGeneration || page.Count == 0)
                {
                    break;
                }

                var fresh = page.Where(p => seenKeys.Add(p.Identifier.Id)).ToList();
                if (fresh.Count == 0)
                {
                    break;
                }

                onPage(fresh);
                applied += fresh.Count;
                if (nextOffset == offset)
                {
                    break;
                }

                offset = nextOffset;
            }
            catch (Exception ex) when (i > 0)
            {
                // 第一页失败交给调用方按刷新失败处理；后续页失败则保留已取到的部分。
                _logger.LogError(ex, "获取推荐视频后续页时出错，保留已取到的部分.");
                break;
            }
        }

        return offset;
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
        if (_feedSource is not null && !allowSourceChange)
        {
            // 滚动加载沿用当前来源，避免把新来源的条目接到旧列表后面。
            return _feedSource;
        }

        // 刷新与首屏重新创建来源：合并来源自己攥着网页端批次，只靠页面游标无法把它重置到头上。
        var kind = SettingsToolkit.ReadLocalSetting(SettingNames.RecommendFeedSource, RecommendFeedSource.Mobile);
        var kindChanged = _feedSource is null || _feedSourceKind != kind;
        _feedSourceKind = kind;
        _feedSource = RecommendFeedSourceFactory.Create(_service, kind);
        if (kindChanged)
        {
            // 只有真的换了来源才作废旧游标；同一来源刷新失败时要保留原游标。
            _recommendOffset = 0;
        }

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