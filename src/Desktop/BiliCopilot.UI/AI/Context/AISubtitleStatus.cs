// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 字幕采集状态.
/// </summary>
internal enum AISubtitleStatus
{
    /// <summary>
    /// 本次请求不需要字幕（例如文章目标）.
    /// </summary>
    NotRequested,

    /// <summary>
    /// 已取得可用的字幕文本.
    /// </summary>
    Available,

    /// <summary>
    /// 没有字幕或字幕明细为空，调用方应继续生成.
    /// </summary>
    Unavailable,
}
