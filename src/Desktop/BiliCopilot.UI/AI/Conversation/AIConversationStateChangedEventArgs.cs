// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// 编排状态变化事件参数.
/// </summary>
public sealed class AIConversationStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="snapshot">变化后的状态快照.</param>
    public AIConversationStateChangedEventArgs(AIConversationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
    }

    /// <summary>
    /// 获取变化后的状态快照.
    /// </summary>
    public AIConversationSnapshot Snapshot { get; }
}
