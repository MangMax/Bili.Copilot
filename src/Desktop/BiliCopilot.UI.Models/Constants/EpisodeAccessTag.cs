// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Models.Constants;

/// <summary>
/// 单集的观看权益标记.
/// </summary>
/// <remarks>
/// 由剧集元数据派生，只用于在列表上提示用户，不代表当前账号的实际播放权限；
/// 能否播放始终以播放地址请求的结果为准.
/// </remarks>
public enum EpisodeAccessTag
{
    /// <summary>
    /// 无法判定（非番剧剧集，或徽标含义未知），不显示角标.
    /// </summary>
    Unspecified,

    /// <summary>
    /// 预告.
    /// </summary>
    Preview,

    /// <summary>
    /// 大会员专享.
    /// </summary>
    Vip,

    /// <summary>
    /// 限时免费.
    /// </summary>
    LimitedFree,

    /// <summary>
    /// 免费.
    /// </summary>
    Free,
}
