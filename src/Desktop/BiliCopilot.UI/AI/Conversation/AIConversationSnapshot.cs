// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// 编排状态快照，视图模型只做镜像与展示映射.
/// </summary>
public sealed record AIConversationSnapshot
{
    /// <summary>
    /// 获取界面展示的请求文本.
    /// </summary>
    public string RequestText { get; init; } = string.Empty;

    /// <summary>
    /// 获取当前进度阶段；没有进行中的阶段时为 <see langword="null"/>.
    /// </summary>
    public AIConversationStage? Stage { get; init; }

    /// <summary>
    /// 获取是否正在生成.
    /// </summary>
    public bool IsGenerating { get; init; }

    /// <summary>
    /// 获取流式过程中的临时文本.
    /// </summary>
    public string TempResult { get; init; } = string.Empty;

    /// <summary>
    /// 获取最终结果.
    /// </summary>
    public string FinalResult { get; init; } = string.Empty;

    /// <summary>
    /// 获取失败消息；没有失败时为空字符串.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// 获取非致命警告.
    /// </summary>
    public AIConversationWarning? Warning { get; init; }

    /// <summary>
    /// 获取失败类型.
    /// </summary>
    public AIConversationErrorKind ErrorKind { get; init; } = AIConversationErrorKind.None;
}
