// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Conversation;
using Microsoft.UI.Dispatching;

namespace BiliCopilot.UI.Services.AI;

/// <summary>
/// 基于界面消息队列的编排调度适配器.
/// </summary>
/// <remarks>
/// 状态更新立即封送到界面线程；流式增量保留 75 毫秒的合并延迟后再封送，
/// 与原来的节流行为一致.
/// </remarks>
internal sealed class DispatcherQueueConversationScheduler : IAIConversationScheduler
{
    private static readonly TimeSpan StreamingInterval = TimeSpan.FromMilliseconds(75);

    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatcherQueueConversationScheduler"/> class.
    /// </summary>
    /// <param name="dispatcherQueue">界面消息队列.</param>
    public DispatcherQueueConversationScheduler(DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        _dispatcherQueue = dispatcherQueue;
    }

    /// <inheritdoc/>
    public void Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _dispatcherQueue.TryEnqueue(() => callback());
    }

    /// <inheritdoc/>
    public void Schedule(Action callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _ = DelayAndPostAsync(callback, cancellationToken);
    }

    private async Task DelayAndPostAsync(Action callback, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(StreamingInterval, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                callback();
            }
        });
    }
}
