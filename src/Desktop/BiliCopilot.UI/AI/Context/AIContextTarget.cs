// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// AI 上下文的目标类型.
/// </summary>
public enum AIContextTarget
{
    /// <summary>
    /// 视频，上下文来自字幕与评论.
    /// </summary>
    Video,

    /// <summary>
    /// 文章，上下文来自正文与评论.
    /// </summary>
    Article,
}
