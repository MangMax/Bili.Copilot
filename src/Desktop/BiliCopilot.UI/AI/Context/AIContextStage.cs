// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 上下文采集阶段，用于向上层报告进度.
/// </summary>
/// <remarks>
/// 核心层只报告阶段，不取本地化文案；展示文案由上层映射.
/// </remarks>
public enum AIContextStage
{
    /// <summary>
    /// 正在获取字幕元数据.
    /// </summary>
    SubtitleMetadata,

    /// <summary>
    /// 正在获取字幕明细.
    /// </summary>
    SubtitleDetail,

    /// <summary>
    /// 正在获取热评.
    /// </summary>
    Comments,

    /// <summary>
    /// 正在提取文章纯文本.
    /// </summary>
    ArticleContent,
}
