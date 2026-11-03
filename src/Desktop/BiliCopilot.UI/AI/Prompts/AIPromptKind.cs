// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Prompts;

/// <summary>
/// AI 提示词的类型.
/// </summary>
public enum AIPromptKind
{
    /// <summary>
    /// 视频总结.
    /// </summary>
    VideoSummary,

    /// <summary>
    /// 视频评价.
    /// </summary>
    VideoEvaluation,

    /// <summary>
    /// 视频提问.
    /// </summary>
    VideoQuestion,

    /// <summary>
    /// 文章总结.
    /// </summary>
    ArticleSummary,

    /// <summary>
    /// 文章评价.
    /// </summary>
    ArticleEvaluation,

    /// <summary>
    /// 文章提问.
    /// </summary>
    ArticleQuestion,
}
