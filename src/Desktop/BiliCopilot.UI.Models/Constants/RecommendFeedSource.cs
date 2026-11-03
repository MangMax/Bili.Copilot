// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Models.Constants;

/// <summary>
/// 推荐流来源.
/// </summary>
public enum RecommendFeedSource
{
    /// <summary>
    /// 手机端推荐流（游标翻页，服务端决定单页条数）.
    /// </summary>
    Mobile,

    /// <summary>
    /// 网页端推荐流（支持声明的页大小，单次可拿更多卡片）.
    /// </summary>
    Web,

    /// <summary>
    /// 同时请求两端并交错合并.
    /// </summary>
    Merged,
}
