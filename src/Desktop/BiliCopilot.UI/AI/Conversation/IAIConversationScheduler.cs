// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// 编排层调度端口：把状态更新与流式发布封送到界面线程.
/// </summary>
/// <remarks>
/// 核心层不直接接触界面线程；适配层用界面消息队列实现，测试用可推进的假调度器实现.
/// </remarks>
public interface IAIConversationScheduler
{
    /// <summary>
    /// 立即把回调封送到界面线程执行.
    /// </summary>
    /// <param name="callback">待执行的回调.</param>
    void Post(Action callback);

    /// <summary>
    /// 延迟一小段时间后再把回调封送到界面线程，用于合并流式增量.
    /// </summary>
    /// <param name="callback">待执行的回调.</param>
    /// <param name="cancellationToken">取消令牌，取消后不再执行回调.</param>
    void Schedule(Action callback, CancellationToken cancellationToken);
}
