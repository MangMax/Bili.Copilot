// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Prompts;

/// <summary>
/// 构建提示词时可用的变量，未提供的变量按提示词类型使用缺省文案或留空。
/// </summary>
public sealed record AIPromptVariables
{
    /// <summary>
    /// 视频或文章标题。
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 视频简介。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 视频字幕文本。
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// 热门评论文本。
    /// </summary>
    public string? Comments { get; init; }

    /// <summary>
    /// 文章正文纯文本。
    /// </summary>
    public string? ArticleContent { get; init; }

    /// <summary>
    /// 用户提出的问题。
    /// </summary>
    public string? Question { get; init; }
}
