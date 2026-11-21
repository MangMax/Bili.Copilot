// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 动态页新内容检测与点击提示刷新时的分区决策.
/// </summary>
public static class MomentRefreshPolicy
{
    /// <summary>
    /// 判断动态是否属于视频分区（普通视频与番剧）.
    /// </summary>
    /// <param name="moment">动态条目.</param>
    /// <returns>属于视频分区时返回 <c>true</c>.</returns>
    public static bool IsVideoMoment(MomentInformation moment)
        => moment.MomentType is MomentItemType.Video or MomentItemType.Pgc
            || (moment.MomentType is null && moment.Data is VideoInformation or EpisodeInformation);

    /// <summary>
    /// 提取动态视图中最大的动态 Id.
    /// </summary>
    /// <param name="view">动态视图.</param>
    /// <returns>最大动态 Id；无有效 Id 时返回 0.</returns>
    public static ulong LatestId(MomentView view)
        => view.Moments.Select(ParseId).DefaultIfEmpty().Max();

    /// <summary>
    /// 从综合动态视图中分别提取全部、视频与非视频的最新动态 Id.
    /// </summary>
    /// <param name="view">综合动态视图.</param>
    /// <returns>全部、视频与非视频的最新动态 Id.</returns>
    public static (ulong LatestAll, ulong LatestVideo, ulong LatestNonVideo) ExtractLatestIds(MomentView view)
    {
        ulong latestAll = 0;
        ulong latestVideo = 0;
        ulong latestNonVideo = 0;
        foreach (var moment in view.Moments)
        {
            var id = ParseId(moment);
            if (id == 0)
            {
                continue;
            }

            if (id > latestAll)
            {
                latestAll = id;
            }

            if (IsVideoMoment(moment))
            {
                if (id > latestVideo)
                {
                    latestVideo = id;
                }
            }
            else if (id > latestNonVideo)
            {
                latestNonVideo = id;
            }
        }

        return (latestAll, latestVideo, latestNonVideo);
    }

    /// <summary>
    /// 决定点击「有新动态，点击刷新」时是否应停留在视频分区刷新.
    /// </summary>
    /// <param name="isCurrentlyVideoSection">当前是否处于视频分区.</param>
    /// <param name="seenMomentId">已读动态水位.</param>
    /// <param name="latestVideoMomentId">已检测到的最新视频动态 Id.</param>
    /// <param name="latestNonVideoMomentId">已检测到的最新非视频动态 Id.</param>
    /// <returns>应刷新视频分区时返回 <c>true</c>，应刷新综合分区时返回 <c>false</c>.</returns>
    public static bool ShouldRefreshVideoSection(
        bool isCurrentlyVideoSection,
        ulong seenMomentId,
        ulong latestVideoMomentId,
        ulong latestNonVideoMomentId)
    {
        if (!isCurrentlyVideoSection)
        {
            // 当前在综合页：综合页同时展示视频与其他动态，留在综合页刷新即可。
            return false;
        }

        // 当前在视频页：只要检测到新视频（含番剧），留在视频页刷新；
        // 仅当新动态全是非视频动态时，才切换到综合页。
        return latestVideoMomentId > seenMomentId || latestNonVideoMomentId <= seenMomentId;
    }

    /// <summary>
    /// 计算刷新完成后应落盘的已读动态水位.
    /// </summary>
    /// <param name="refreshedVideoSection">本次刷新的是否为视频分区.</param>
    /// <param name="previousSeen">刷新前已记录的已读水位.</param>
    /// <param name="refreshedLatestId">本次刷新返回的最新动态 Id.</param>
    /// <param name="checkedLatestAll">发起本次刷新前已检测到的最新全部动态 Id.</param>
    /// <param name="checkedLatestVideo">发起本次刷新前已检测到的最新视频动态 Id.</param>
    /// <returns>刷新后应保存的已读水位.</returns>
    public static ulong ResolveSeenAfterRefresh(
        bool refreshedVideoSection,
        ulong previousSeen,
        ulong refreshedLatestId,
        ulong checkedLatestAll,
        ulong checkedLatestVideo)
    {
        var seen = Math.Max(previousSeen, refreshedLatestId);
        if (refreshedVideoSection && refreshedLatestId >= checkedLatestVideo)
        {
            // 视频页已成功拉取到本次检测到的新视频，同步清掉本轮提示；
            // 若刷新期间又检测到更新的动态（_latestMomentId > checkedLatestAll），仍会保留提示。
            seen = Math.Max(seen, checkedLatestAll);
        }

        return seen;
    }

    private static ulong ParseId(MomentInformation moment)
        => ulong.TryParse(moment.Id, out var id) ? id : 0UL;
}
