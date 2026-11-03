// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Prompts;

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// 一次 AI 请求的描述：动作类型、源快照、提示词模板与问题。
/// </summary>
/// <remarks>
/// 六个入口（视频总结/评价/提问、文章总结/评价/提问）都收敛到同一份请求描述，
/// 由 <see cref="AIConversationOrchestrator"/> 统一采集上下文、构建提示词并执行请求。
/// </remarks>
public sealed record AIConversationRequest
{
    /// <summary>
    /// 获取请求类型，决定上下文采集范围与提示词占位符。
    /// </summary>
    public AIPromptKind Kind { get; init; }

    /// <summary>
    /// 获取源快照。
    /// </summary>
    public AIConversationSource Source { get; init; } = new();

    /// <summary>
    /// 获取提示词模板。
    /// </summary>
    public string Template { get; init; } = string.Empty;

    /// <summary>
    /// 获取用户提出的问题；非提问类请求忽略该值。
    /// </summary>
    public string? Question { get; init; }

    /// <summary>
    /// 获取界面展示的请求文本（例如“总结《标题》的内容”）。
    /// </summary>
    public string RequestText { get; init; } = string.Empty;
}
