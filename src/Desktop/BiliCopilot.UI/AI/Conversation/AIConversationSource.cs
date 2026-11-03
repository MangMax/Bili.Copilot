// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Context;

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// 一次 AI 请求使用的源快照：只包含拼提示词所需的信息，不携带页面模型.
/// </summary>
public sealed record AIConversationSource
{
    /// <summary>
    /// 获取目标类型；视频使用字幕与评论，文章使用正文与评论.
    /// </summary>
    public AIContextTarget Target { get; init; } = AIContextTarget.Video;

    /// <summary>
    /// 获取视频 Id 或文章 Id.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 获取视频分 P 的 Id；文章目标忽略该值.
    /// </summary>
    public string? PartId { get; init; }

    /// <summary>
    /// 获取标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 获取视频简介，可能为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 获取文章 HTML 内容；视频目标忽略该值.
    /// </summary>
    public string? HtmlContent { get; init; }
}
