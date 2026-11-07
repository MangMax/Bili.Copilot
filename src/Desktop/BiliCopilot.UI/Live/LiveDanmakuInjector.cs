// Copyright (c) Bili Copilot. All rights reserved.

using System.Threading.Channels;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播弹幕注入层：批量消费事件通道，交给准入策略，并按固定节拍一次性交付给弹幕控件。
/// </summary>
/// <remarks>
/// 每个节拍最多调用一次注入回调（<see cref="Action{T}"/>，参数为本批文本），
/// 绝不按消息逐条调度 UI。关键词高亮在进入准入前判定；醒目留言作为最高优先级候选进入同一队列。
/// </remarks>
public sealed class LiveDanmakuInjector
{
    /// <summary>
    /// 默认注入节拍（毫秒）：落在 16–50ms 的固定节奏内。
    /// </summary>
    public static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>
    /// 单次节拍最多注入的条数，避免优先级洪水时一帧塞爆渲染器。
    /// </summary>
    public const int DefaultMaxInjectionsPerFlush = 16;

    private readonly LiveDanmakuAdmission _admission;
    private readonly Action<IReadOnlyList<string>> _injectBatch;
    private readonly Func<string, bool>? _keywordHighlight;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _flushInterval;
    private readonly int _maxInjectionsPerFlush;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuInjector"/> class.
    /// </summary>
    /// <param name="admission">准入策略.</param>
    /// <param name="injectBatch">把一批文本交给弹幕控件的回调；由调用方负责切到 UI 线程.</param>
    /// <param name="keywordHighlight">关键词高亮判定；为空表示不高亮.</param>
    /// <param name="timeProvider">时间来源；测试可传入假时钟.</param>
    /// <param name="flushInterval">注入节拍.</param>
    /// <param name="maxInjectionsPerFlush">单次节拍最多注入条数.</param>
    public LiveDanmakuInjector(
        LiveDanmakuAdmission admission,
        Action<IReadOnlyList<string>> injectBatch,
        Func<string, bool>? keywordHighlight = null,
        TimeProvider? timeProvider = null,
        TimeSpan? flushInterval = null,
        int maxInjectionsPerFlush = DefaultMaxInjectionsPerFlush)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(injectBatch);
        if (maxInjectionsPerFlush < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInjectionsPerFlush), "单次注入条数必须大于零。");
        }

        var interval = flushInterval ?? DefaultFlushInterval;
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(flushInterval), "注入节拍必须大于零。");
        }

        _admission = admission;
        _injectBatch = injectBatch;
        _keywordHighlight = keywordHighlight;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _flushInterval = interval;
        _maxInjectionsPerFlush = maxInjectionsPerFlush;
    }

    /// <summary>
    /// Gets 本注入层使用的准入策略.
    /// </summary>
    public LiveDanmakuAdmission Admission => _admission;

    /// <summary>
    /// 把一个内核事件转成准入候选；不产出事件的类型直接忽略。
    /// </summary>
    /// <param name="liveEvent">内核事件.</param>
    /// <param name="now">当前时间.</param>
    public void Submit(LiveDanmakuEvent liveEvent, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(liveEvent);
        switch (liveEvent)
        {
            case LiveDanmakuReceivedEvent received:
                var danmaku = received.Danmaku;
                _admission.Submit(
                    new LiveDanmakuCandidate
                    {
                        Text = danmaku.Text,
                        UserName = danmaku.UserName,
                        GuardLevel = danmaku.GuardLevel,
                        IsAdmin = danmaku.IsAdmin,
                        IsVip = danmaku.IsVip,
                        IsKeywordHighlighted = _keywordHighlight?.Invoke(danmaku.Text) == true,
                    },
                    now);
                break;
            case LiveSuperChatEvent superChat:
                _admission.Submit(
                    new LiveDanmakuCandidate
                    {
                        Text = superChat.SuperChat.Message,
                        UserName = superChat.SuperChat.UserName,
                        IsSuperChat = true,
                    },
                    now);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 把通道里当前可用的事件全部转成候选。
    /// </summary>
    /// <param name="reader">事件通道.</param>
    /// <param name="now">当前时间.</param>
    /// <returns>本次处理的事件数.</returns>
    public int SubmitAvailable(ChannelReader<LiveDanmakuEvent> reader, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var count = 0;
        while (reader.TryRead(out var liveEvent))
        {
            Submit(liveEvent, now);
            count++;
        }

        return count;
    }

    /// <summary>
    /// 取出准入队列中的一批弹幕并一次性交付。
    /// </summary>
    /// <param name="now">当前时间.</param>
    /// <returns>本次交付的条数.</returns>
    public int Flush(DateTimeOffset now)
    {
        var batch = _admission.Drain(now, _maxInjectionsPerFlush);
        if (batch.Count == 0)
        {
            return 0;
        }

        _injectBatch(batch);
        return batch.Count;
    }

    /// <summary>
    /// 按固定节拍批量消费事件通道，直到取消或通道结束。
    /// </summary>
    /// <param name="reader">事件通道.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>消费任务.</returns>
    public async Task RunAsync(ChannelReader<LiveDanmakuEvent> reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        using var timer = new PeriodicTimer(_flushInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var now = _timeProvider.GetUtcNow();
                SubmitAvailable(reader, now);
                Flush(now);
                if (reader.Completion.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
