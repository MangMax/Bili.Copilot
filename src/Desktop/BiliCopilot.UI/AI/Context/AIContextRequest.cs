// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 上下文采集请求.
/// </summary>
public sealed record AIContextRequest
{
    /// <summary>
    /// 目标类型.
    /// </summary>
    public AIContextTarget Target { get; init; }

    /// <summary>
    /// 视频 Id 或文章 Id，用于获取评论.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 视频分 P 的 Id；文章目标忽略该值.
    /// </summary>
    public string? PartId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 简介，可能为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 文章 HTML 内容；视频目标忽略该值.
    /// </summary>
    public string? HtmlContent { get; init; }

    /// <summary>
    /// 是否采集热评.
    /// </summary>
    public bool IncludeComments { get; init; }
}
