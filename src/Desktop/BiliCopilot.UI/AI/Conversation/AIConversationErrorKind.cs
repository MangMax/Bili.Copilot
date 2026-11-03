// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// 编排失败的类型，视图模型据此决定提示文案与是否弹窗。
/// </summary>
public enum AIConversationErrorKind
{
    /// <summary>
    /// 没有失败。
    /// </summary>
    None,

    /// <summary>
    /// 文章正文为空，未发起模型请求；文案由视图模型映射。
    /// </summary>
    ArticleContentEmpty,

    /// <summary>
    /// 模型返回空响应。
    /// </summary>
    EmptyResponse,

    /// <summary>
    /// 模型服务初始化失败。
    /// </summary>
    InitializationFailure,

    /// <summary>
    /// 模型请求过程中的其他失败。
    /// </summary>
    ChatFailure,

    /// <summary>
    /// 上下文采集失败（字幕或评论）。
    /// </summary>
    ContextFailure,
}
