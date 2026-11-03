// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// 编排进度阶段。
/// </summary>
/// <remarks>
/// 核心层只报告阶段，具体文案由视图模型映射本地化字符串。
/// </remarks>
public enum AIConversationStage
{
    /// <summary>
    /// 正在获取字幕元数据。
    /// </summary>
    SubtitleMetadata,

    /// <summary>
    /// 正在获取字幕文本内容。
    /// </summary>
    SubtitleDetail,

    /// <summary>
    /// 正在获取热门评论。
    /// </summary>
    Comments,

    /// <summary>
    /// 正在提取文章正文。
    /// </summary>
    ArticleContent,

    /// <summary>
    /// 正在总结内容。
    /// </summary>
    Summarizing,

    /// <summary>
    /// 正在生成回答。
    /// </summary>
    Generating,
}
