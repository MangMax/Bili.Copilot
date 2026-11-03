// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 一次 AI 请求可用的源上下文.
/// </summary>
internal sealed record AISourceContext
{
    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 简介，可能为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 字幕文本，多行；没有字幕时为空字符串.
    /// </summary>
    public string SubtitleText { get; init; } = string.Empty;

    /// <summary>
    /// 字幕采集状态.
    /// </summary>
    public AISubtitleStatus SubtitleStatus { get; init; } = AISubtitleStatus.NotRequested;

    /// <summary>
    /// 热评文本，可能为空字符串.
    /// </summary>
    public string CommentText { get; init; } = string.Empty;

    /// <summary>
    /// 文章纯文本；视频目标为空字符串.
    /// </summary>
    public string ArticleText { get; init; } = string.Empty;
}
