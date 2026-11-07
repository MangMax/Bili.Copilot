// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 从剧集元数据推断观看权益角标.
/// </summary>
/// <remarks>
/// 只用列表阶段已有的标记，不额外请求接口，也不代表当前账号的真实播放权限.
/// </remarks>
internal static class EpisodeAccessTagClassifier
{
    private const string PreviewBadge = "预告";
    private const string VipBadge = "会员";
    private const string LimitedFreeBadge = "限免";

    /// <summary>
    /// 分类单集的权益标记.
    /// </summary>
    /// <param name="isPgc">是否来自番剧剧集（缺少该来源时不做推断）.</param>
    /// <param name="isPreview">是否为预告片.</param>
    /// <param name="isVip">是否为会员专属.</param>
    /// <param name="badgeText">原始徽标文本.</param>
    /// <returns>观看权益标记.</returns>
    public static EpisodeAccessTag Classify(bool isPgc, bool isPreview, bool isVip, string? badgeText)
    {
        if (!isPgc)
        {
            return EpisodeAccessTag.Unspecified;
        }

        var badge = badgeText?.Trim() ?? string.Empty;
        if (isPreview || badge.Contains(PreviewBadge, StringComparison.Ordinal))
        {
            return EpisodeAccessTag.Preview;
        }

        if (isVip || badge.Contains(VipBadge, StringComparison.Ordinal))
        {
            return EpisodeAccessTag.Vip;
        }

        if (badge.Contains(LimitedFreeBadge, StringComparison.Ordinal))
        {
            return EpisodeAccessTag.LimitedFree;
        }

        if (badge.Length == 0)
        {
            return EpisodeAccessTag.Free;
        }

        return EpisodeAccessTag.Unspecified;
    }
}
