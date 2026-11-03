// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;
using BiliCopilot.UI.AI.Context;
using BiliCopilot.UI.AI.Conversation;

namespace BiliCopilot.UI.Tests.Support;

/// <summary>
/// 配置快照读取端口的假件：返回固定快照，并记录读取次数。
/// </summary>
internal sealed class FakeSnapshotReader(AIChatConfigSnapshot snapshot) : IAIChatConfigSnapshotReader
{
    /// <summary>
    /// 获取或设置读取时返回的快照，可在请求中途替换以模拟配置切换。
    /// </summary>
    public AIChatConfigSnapshot Snapshot { get; set; } = snapshot;

    /// <summary>
    /// 获取读取被调用的次数。
    /// </summary>
    public int ReadCount { get; private set; }

    /// <inheritdoc/>
    public Task<AIChatConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult(Snapshot);
    }
}

/// <summary>
/// 配置快照读取端口的假件：读取时抛出异常，模拟尚未选择服务或模型。
/// </summary>
internal sealed class ThrowingSnapshotReader : IAIChatConfigSnapshotReader
{
    /// <summary>
    /// 初始化 <see cref="ThrowingSnapshotReader"/> 的新实例。
    /// </summary>
    /// <param name="message">读取时抛出的异常消息。</param>
    public ThrowingSnapshotReader(string message) => Message = message;

    /// <summary>
    /// 获取读取时抛出的异常消息。
    /// </summary>
    public string Message { get; }

    /// <inheritdoc/>
    public Task<AIChatConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
        => Task.FromException<AIChatConfigSnapshot>(new InvalidOperationException(Message));
}

/// <summary>
/// 模型客户端端口的假件：记录初始化与请求时的快照、提示词与增量回调。
/// </summary>
internal sealed class FakeChatModelClient(
    Func<AIChatConfigSnapshot, string, Action<string>?, CancellationToken, Task<string>> complete) : IAIChatModelClient
{
    /// <summary>
    /// 获取初始化时收到的快照。
    /// </summary>
    public AIChatConfigSnapshot? InitializedWith { get; private set; }

    /// <summary>
    /// 获取请求时收到的快照。
    /// </summary>
    public AIChatConfigSnapshot? CompletedWith { get; private set; }

    /// <summary>
    /// 获取最近一次请求收到的提示词。
    /// </summary>
    public string? LastPrompt { get; private set; }

    /// <summary>
    /// 获取或设置初始化时抛出的异常，为 <see langword="null"/> 表示初始化成功。
    /// </summary>
    public Exception? InitializationError { get; set; }

    /// <summary>
    /// 获取最近一次请求是否收到增量回调。
    /// </summary>
    public bool ReceivedStreamingCallback { get; private set; }

    /// <summary>
    /// 获取是否已完成初始化。
    /// </summary>
    public bool Initialized => InitializedWith is not null;

    /// <inheritdoc/>
    public Task InitializeAsync(AIChatConfigSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        InitializedWith = snapshot;
        return InitializationError is null
            ? Task.CompletedTask
            : Task.FromException(InitializationError);
    }

    /// <inheritdoc/>
    public Task<string> CompleteAsync(
        AIChatConfigSnapshot snapshot,
        string prompt,
        Action<string>? onDelta,
        CancellationToken cancellationToken = default)
    {
        CompletedWith = snapshot;
        LastPrompt = prompt;
        ReceivedStreamingCallback = onDelta is not null;
        return complete(snapshot, prompt, onDelta, cancellationToken);
    }
}

/// <summary>
/// 界面线程调度器端口的假件：把回调排队，由测试显式驱动。
/// </summary>
internal sealed class QueueScheduler : IAIConversationScheduler
{
    /// <summary>
    /// 获取尚未执行的回调队列。
    /// </summary>
    public Queue<Action> Pending { get; } = new();

    /// <inheritdoc/>
    public void Post(Action callback) => Pending.Enqueue(callback);

    /// <inheritdoc/>
    public void Schedule(Action callback, CancellationToken cancellationToken) => Pending.Enqueue(callback);

    /// <summary>
    /// 依次执行队列中的全部回调。
    /// </summary>
    public void Drain()
    {
        while (Pending.Count > 0)
        {
            Pending.Dequeue()();
        }
    }
}

/// <summary>
/// 流式发布目标的假件：把调度动作排队，把发布结果按顺序记录。
/// </summary>
internal sealed class QueueStreamTarget
{
    /// <summary>
    /// 初始化 <see cref="QueueStreamTarget"/> 的新实例。
    /// </summary>
    public QueueStreamTarget()
    {
        Target = new AIChatStreamTarget(Pending.Enqueue, (text, final) => Published.Add((text, final)));
    }

    /// <summary>
    /// 获取尚未执行的调度动作队列。
    /// </summary>
    public Queue<Action> Pending { get; } = new();

    /// <summary>
    /// 获取已发布的文本与是否为最终结果。
    /// </summary>
    public List<(string Text, bool Final)> Published { get; } = new();

    /// <summary>
    /// 获取被测的发布目标。
    /// </summary>
    public AIChatStreamTarget Target { get; }

    /// <summary>
    /// 依次执行队列中的全部调度动作。
    /// </summary>
    public void Drain()
    {
        while (Pending.Count > 0)
        {
            Pending.Dequeue()();
        }
    }
}

/// <summary>
/// 字幕来源端口的假件：返回固定轨道与明细，并记录调用情况。
/// </summary>
internal sealed class FakeSubtitleSource(
    IReadOnlyList<AISubtitleTrack> tracks,
    IReadOnlyList<AISubtitleCue> cues) : IAISubtitleSource
{
    /// <summary>
    /// 获取或设置读取字幕明细时执行的回调，用于模拟采集中途取消。
    /// </summary>
    public Action? OnGetCues { get; init; }

    /// <summary>
    /// 获取字幕元数据被调用的次数。
    /// </summary>
    public int TrackCallCount { get; private set; }

    /// <summary>
    /// 获取最近一次被请求明细的轨道标识。
    /// </summary>
    public string? LastRequestedTrackId { get; private set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AISubtitleTrack>> GetTracksAsync(string videoId, string? partId, CancellationToken cancellationToken = default)
    {
        TrackCallCount++;
        return Task.FromResult(tracks);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AISubtitleCue>> GetCuesAsync(AISubtitleTrack track, CancellationToken cancellationToken = default)
    {
        LastRequestedTrackId = track.Id;
        OnGetCues?.Invoke();
        return Task.FromResult(cues);
    }
}

/// <summary>
/// 评论来源端口的假件：返回固定热评，并记录调用情况。
/// </summary>
internal sealed class FakeCommentSource(IReadOnlyList<AIComment> comments) : IAICommentSource
{
    /// <summary>
    /// 获取或设置获取热评时执行的回调，用于模拟采集中途取消。
    /// </summary>
    public Action? OnGetComments { get; init; }

    /// <summary>
    /// 获取热评被调用的次数。
    /// </summary>
    public int CallCount { get; private set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AIComment>> GetHotCommentsAsync(string targetId, AIContextTarget target, CancellationToken cancellationToken = default)
    {
        CallCount++;
        OnGetComments?.Invoke();
        return Task.FromResult(comments);
    }
}

/// <summary>
/// 采集进度的假件：按上报顺序记录阶段。
/// </summary>
internal sealed class RecordingProgress : IProgress<AIContextStage>
{
    /// <summary>
    /// 获取已上报的采集阶段。
    /// </summary>
    public List<AIContextStage> Stages { get; } = [];

    /// <inheritdoc/>
    public void Report(AIContextStage value) => Stages.Add(value);
}
