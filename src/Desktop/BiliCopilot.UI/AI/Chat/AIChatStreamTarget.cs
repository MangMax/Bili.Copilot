// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 流式结果发布目标：调度与发布动作由调用方注入.
/// </summary>
/// <remarks>
/// 核心层只负责按 <see cref="BiliCopilot.UI.Toolkits.StreamingResponseSession"/> 的批处理语义调用
/// <see cref="Schedule"/> 与 <see cref="Publish"/>，不关心 UI 线程或调度延迟.
/// </remarks>
internal sealed class AIChatStreamTarget
{
    private readonly Action<Action> _schedule;
    private readonly Action<string, bool> _publish;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatStreamTarget"/> class.
    /// </summary>
    /// <param name="schedule">调度一次待执行发布动作.</param>
    /// <param name="publish">发布文本与是否为最终结果.</param>
    public AIChatStreamTarget(Action<Action> schedule, Action<string, bool> publish)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(publish);
        _schedule = schedule;
        _publish = publish;
    }

    /// <summary>
    /// 调度一次发布动作.
    /// </summary>
    /// <param name="callback">待执行的发布动作.</param>
    public void Schedule(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _schedule(callback);
    }

    /// <summary>
    /// 立即发布文本.
    /// </summary>
    /// <param name="text">当前完整文本.</param>
    /// <param name="isFinal">是否为最终结果.</param>
    public void Publish(string text, bool isFinal) => _publish(text, isFinal);
}
