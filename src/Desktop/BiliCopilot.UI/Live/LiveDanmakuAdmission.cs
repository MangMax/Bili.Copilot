// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播弹幕优先级，数值越大越优先。
/// </summary>
/// <remarks>
/// <para>内核的 <c>GuardLevel</c> 编码是 1 = 总督、2 = 提督、3 = 舰长，数值越小等级越高；
/// 准入优先级则相反（数值越大越优先），因此必须按语义映射而不是直接比较内核数值：
/// 总督 &gt; 提督 &gt; 舰长。</para>
/// <para>任何大航海都高于管理员/老爷/关键词高亮，高亮高于普通弹幕；醒目留言最高。</para>
/// <para>已知缺口（#192，不臆造协议）：置顶弹幕没有可用的协议信号，因此没有独立的优先级层，
/// 它由大航海/管理员/关键词高亮三层覆盖。</para>
/// </remarks>
public enum LiveDanmakuPriority
{
    /// <summary>
    /// 普通弹幕.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 管理员、老爷或命中关键词的弹幕.
    /// </summary>
    Highlighted = 10,

    /// <summary>
    /// 舰长（内核 GuardLevel = 3，大航海中最末一档）.
    /// </summary>
    Captain = 21,

    /// <summary>
    /// 提督（内核 GuardLevel = 2）.
    /// </summary>
    Admiral = 22,

    /// <summary>
    /// 总督（内核 GuardLevel = 1，大航海中的最高档）.
    /// </summary>
    Governor = 23,

    /// <summary>
    /// 醒目留言.
    /// </summary>
    SuperChat = 30,
}

/// <summary>
/// 进入准入的一条直播弹幕。
/// </summary>
public readonly record struct LiveDanmakuCandidate
{
    /// <summary>
    /// Gets 文本（醒目留言为留言内容）.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets 发送者昵称；仅用于日志与后续聊天列表，不参与准入.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Gets 舰长等级：0 无，1 总督，2 提督，3 舰长.
    /// </summary>
    public int GuardLevel { get; init; }

    /// <summary>
    /// Gets 是否为管理员.
    /// </summary>
    public bool IsAdmin { get; init; }

    /// <summary>
    /// Gets 是否为老爷/年费老爷.
    /// </summary>
    public bool IsVip { get; init; }

    /// <summary>
    /// Gets 是否命中关键词高亮.
    /// </summary>
    public bool IsKeywordHighlighted { get; init; }

    /// <summary>
    /// Gets 是否为醒目留言.
    /// </summary>
    public bool IsSuperChat { get; init; }

    /// <summary>
    /// Gets 该候选的准入优先级.
    /// </summary>
    /// <remarks>
    /// 内核编码 1 = 总督、2 = 提督、3 = 舰长，所以这里按"等级越高优先级越大"重新映射；
    /// 任何大航海都排在管理员/老爷/关键词之前。
    /// </remarks>
    public LiveDanmakuPriority Priority
        => IsSuperChat
            ? LiveDanmakuPriority.SuperChat
            : GuardLevel switch
            {
                1 => LiveDanmakuPriority.Governor,
                2 => LiveDanmakuPriority.Admiral,
                >= 3 => LiveDanmakuPriority.Captain,
                _ => IsAdmin || IsVip || IsKeywordHighlighted
                    ? LiveDanmakuPriority.Highlighted
                    : LiveDanmakuPriority.Normal,
            };
}

/// <summary>
/// 准入策略参数。
/// </summary>
public sealed record LiveDanmakuAdmissionOptions
{
    /// <summary>
    /// Gets 默认参数。
    /// </summary>
    public static LiveDanmakuAdmissionOptions Default { get; } = new();

    /// <summary>
    /// Gets 有界队列容量；满时按优先级淘汰。
    /// </summary>
    public int QueueCapacity { get; init; } = 200;

    /// <summary>
    /// Gets 相同文案的合并窗口。
    /// </summary>
    public TimeSpan MergeWindow { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets 普通弹幕每秒准入上限；舰长/高亮/醒目留言不受该上限约束。
    /// </summary>
    public int NormalAdmissionsPerSecond { get; init; } = 20;

    /// <summary>
    /// Gets 排队超时；超时后丢弃。
    /// </summary>
    public TimeSpan EnqueueTimeout { get; init; } = TimeSpan.FromSeconds(3);
}

/// <summary>
/// 准入计数快照。
/// </summary>
/// <param name="Received">收到的候选数.</param>
/// <param name="Shown">实际上屏数.</param>
/// <param name="Merged">被合并的同文案数.</param>
/// <param name="Blocked">被屏蔽（空文本）数.</param>
/// <param name="RateDropped">速率上限丢弃数.</param>
/// <param name="QueueDropped">队列满淘汰数.</param>
/// <param name="TimeoutDropped">排队超时丢弃数.</param>
public readonly record struct LiveDanmakuCounters(
    long Received,
    long Shown,
    long Merged,
    long Blocked,
    long RateDropped,
    long QueueDropped,
    long TimeoutDropped);

/// <summary>
/// 一次准入的结果。
/// </summary>
public enum LiveDanmakuAdmissionAction
{
    /// <summary>
    /// 已进入队列.
    /// </summary>
    Enqueued,

    /// <summary>
    /// 已并入同文案的计数.
    /// </summary>
    Merged,

    /// <summary>
    /// 空文本被屏蔽.
    /// </summary>
    Blocked,

    /// <summary>
    /// 超过每秒准入上限被丢弃.
    /// </summary>
    RateDropped,

    /// <summary>
    /// 队列已满且没有更低优先级可淘汰.
    /// </summary>
    QueueDropped,

    /// <summary>
    /// 排队超时被丢弃.
    /// </summary>
    TimeoutDropped,
}

/// <summary>
/// 一次准入的判定结果。
/// </summary>
/// <param name="Action">判定结果.</param>
/// <param name="MergedCount">合并后该文案的总数；仅在 <see cref="LiveDanmakuAdmissionAction.Merged"/> 时有效.</param>
public readonly record struct LiveDanmakuAdmissionResult(LiveDanmakuAdmissionAction Action, int MergedCount = 0);

/// <summary>
/// 直播弹幕准入：优先级、同文案短窗合并、每秒限速、有界队列按优先级淘汰与排队超时。
/// </summary>
/// <remarks>
/// 纯逻辑：时间由调用方传入，不读取真实时钟，也不依赖 WinUI。
/// <see cref="Submit"/> 与 <see cref="Drain"/> 必须由同一个消费线程调用；<see cref="Snapshot"/> 可从任意线程读取。
/// 合并分两层：仍在队列中的同文案直接累加计数（上屏时显示为 <c>文本 ×N</c>）；
/// 已上屏且仍在合并窗口内的同文案只计入合并数，不重复上屏，从而把刷屏压到每个窗口一次。
/// 已知缺口（#192，不臆造协议）：置顶弹幕在当前内核版本没有可用的协议信号，
/// 因此没有独立的优先级层；置顶内容由大航海/管理员/关键词高亮三层覆盖，不单独排序。
/// </remarks>
public sealed class LiveDanmakuAdmission
{
    private readonly LiveDanmakuAdmissionOptions _options;
    private readonly List<Entry> _queue = [];
    private readonly Dictionary<string, DateTimeOffset> _recentlyShown = new(StringComparer.Ordinal);

    private long _received;
    private long _shown;
    private long _merged;
    private long _blocked;
    private long _rateDropped;
    private long _queueDropped;
    private long _timeoutDropped;

    private double _tokens;
    private DateTimeOffset _lastRefill;
    private bool _rateInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuAdmission"/> class.
    /// </summary>
    /// <param name="options">准入参数；为空时使用默认值.</param>
    public LiveDanmakuAdmission(LiveDanmakuAdmissionOptions? options = null)
    {
        _options = options ?? LiveDanmakuAdmissionOptions.Default;
        if (_options.QueueCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "队列容量必须大于零。");
        }
    }

    /// <summary>
    /// Gets 当前排队等待上屏的条数。
    /// </summary>
    public int PendingCount => _queue.Count;

    /// <summary>
    /// 尝试准入一条弹幕。
    /// </summary>
    /// <param name="candidate">候选弹幕.</param>
    /// <param name="now">当前时间.</param>
    /// <returns>判定结果.</returns>
    public LiveDanmakuAdmissionResult Submit(LiveDanmakuCandidate candidate, DateTimeOffset now)
    {
        Interlocked.Increment(ref _received);
        var text = Normalize(candidate.Text);
        if (text.Length == 0)
        {
            Interlocked.Increment(ref _blocked);
            return new LiveDanmakuAdmissionResult(LiveDanmakuAdmissionAction.Blocked);
        }

        PurgeExpired(now);

        if (!candidate.IsSuperChat)
        {
            if (TryMergeIntoPending(text, candidate, now, out var mergedCount))
            {
                Interlocked.Increment(ref _merged);
                return new LiveDanmakuAdmissionResult(LiveDanmakuAdmissionAction.Merged, mergedCount);
            }

            if (_recentlyShown.TryGetValue(text, out var shownAt)
                && now >= shownAt
                && now - shownAt <= _options.MergeWindow)
            {
                // 已上屏的同一文案仍在合并窗口内：只计合并，不重复上屏。
                _recentlyShown[text] = now;
                Interlocked.Increment(ref _merged);
                return new LiveDanmakuAdmissionResult(LiveDanmakuAdmissionAction.Merged);
            }
        }

        if (candidate.Priority == LiveDanmakuPriority.Normal && !TryTakeNormalToken(now))
        {
            Interlocked.Increment(ref _rateDropped);
            return new LiveDanmakuAdmissionResult(LiveDanmakuAdmissionAction.RateDropped);
        }

        if (_queue.Count >= _options.QueueCapacity)
        {
            var lowest = FindLowestPriorityIndex();
            if (lowest < 0 || _queue[lowest].Priority >= candidate.Priority)
            {
                Interlocked.Increment(ref _queueDropped);
                return new LiveDanmakuAdmissionResult(LiveDanmakuAdmissionAction.QueueDropped);
            }

            _queue.RemoveAt(lowest);
            Interlocked.Increment(ref _queueDropped);
        }

        _queue.Add(new Entry(text, candidate.Priority, now));
        return new LiveDanmakuAdmissionResult(LiveDanmakuAdmissionAction.Enqueued);
    }

    /// <summary>
    /// 取出一批可上屏的弹幕，按优先级从高到低、同级先入先出。
    /// </summary>
    /// <param name="now">当前时间.</param>
    /// <param name="maxCount">本次最多取出的条数.</param>
    /// <returns>可直接交给弹幕控件的文本.</returns>
    public IReadOnlyList<string> Drain(DateTimeOffset now, int maxCount)
    {
        PurgeExpired(now);
        if (maxCount <= 0 || _queue.Count == 0)
        {
            CleanupRecentlyShown(now);
            return [];
        }

        var batch = new List<string>(Math.Min(maxCount, _queue.Count));
        while (batch.Count < maxCount && _queue.Count > 0)
        {
            var index = FindHighestPriorityIndex();
            var entry = _queue[index];
            _queue.RemoveAt(index);
            Interlocked.Increment(ref _shown);
            _recentlyShown[entry.NormalizedText] = now;
            batch.Add(entry.Count > 1 ? $"{entry.Text} ×{entry.Count}" : entry.Text);
        }

        CleanupRecentlyShown(now);
        return batch;
    }

    /// <summary>
    /// 读取计数快照；可从任意线程调用。
    /// </summary>
    /// <returns>计数快照.</returns>
    public LiveDanmakuCounters Snapshot()
        => new(
            Interlocked.Read(ref _received),
            Interlocked.Read(ref _shown),
            Interlocked.Read(ref _merged),
            Interlocked.Read(ref _blocked),
            Interlocked.Read(ref _rateDropped),
            Interlocked.Read(ref _queueDropped),
            Interlocked.Read(ref _timeoutDropped));

    /// <summary>
    /// 清空排队与合并窗口；关闭/换房时调用。
    /// </summary>
    public void Clear()
    {
        _queue.Clear();
        _recentlyShown.Clear();
        _rateInitialized = false;
        _tokens = 0;
    }

    private static string Normalize(string? text) => text?.Trim() ?? string.Empty;

    private bool TryMergeIntoPending(string text, LiveDanmakuCandidate candidate, DateTimeOffset now, out int count)
    {
        foreach (var entry in _queue)
        {
            if (entry.Priority == LiveDanmakuPriority.SuperChat
                || !string.Equals(entry.NormalizedText, text, StringComparison.Ordinal))
            {
                continue;
            }

            if (now < entry.LastSeenAt || now - entry.LastSeenAt > _options.MergeWindow)
            {
                continue;
            }

            entry.Increment(candidate.Priority, now);
            count = entry.Count;
            return true;
        }

        count = 0;
        return false;
    }

    private bool TryTakeNormalToken(DateTimeOffset now)
    {
        var rate = _options.NormalAdmissionsPerSecond;
        if (rate <= 0)
        {
            return false;
        }

        if (!_rateInitialized)
        {
            _tokens = rate;
            _lastRefill = now;
            _rateInitialized = true;
        }
        else if (now > _lastRefill)
        {
            _tokens = Math.Min(rate, _tokens + ((now - _lastRefill).TotalSeconds * rate));
            _lastRefill = now;
        }

        if (_tokens < 1)
        {
            return false;
        }

        _tokens -= 1;
        return true;
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        if (_options.EnqueueTimeout <= TimeSpan.Zero)
        {
            return;
        }

        for (var i = _queue.Count - 1; i >= 0; i--)
        {
            var entry = _queue[i];
            if (now < entry.EnqueuedAt || now - entry.EnqueuedAt <= _options.EnqueueTimeout)
            {
                continue;
            }

            _queue.RemoveAt(i);
            Interlocked.Increment(ref _timeoutDropped);
        }
    }

    private void CleanupRecentlyShown(DateTimeOffset now)
    {
        if (_recentlyShown.Count == 0)
        {
            return;
        }

        List<string>? expired = null;
        foreach (var item in _recentlyShown)
        {
            if (now >= item.Value && now - item.Value <= _options.MergeWindow)
            {
                continue;
            }

            expired ??= [];
            expired.Add(item.Key);
        }

        if (expired is null)
        {
            return;
        }

        foreach (var key in expired)
        {
            _recentlyShown.Remove(key);
        }
    }

    private int FindHighestPriorityIndex()
    {
        var index = 0;
        for (var i = 1; i < _queue.Count; i++)
        {
            if (_queue[i].Priority > _queue[index].Priority)
            {
                index = i;
            }
            else if (_queue[i].Priority == _queue[index].Priority
                && _queue[i].EnqueuedAt < _queue[index].EnqueuedAt)
            {
                index = i;
            }
        }

        return index;
    }

    private int FindLowestPriorityIndex()
    {
        if (_queue.Count == 0)
        {
            return -1;
        }

        var index = 0;
        for (var i = 1; i < _queue.Count; i++)
        {
            if (_queue[i].Priority < _queue[index].Priority)
            {
                index = i;
            }
            else if (_queue[i].Priority == _queue[index].Priority
                && _queue[i].EnqueuedAt < _queue[index].EnqueuedAt)
            {
                index = i;
            }
        }

        return index;
    }

    private sealed class Entry(string text, LiveDanmakuPriority priority, DateTimeOffset enqueuedAt)
    {
        public string Text { get; } = text;

        public string NormalizedText { get; } = text;

        public LiveDanmakuPriority Priority { get; private set; } = priority;

        public DateTimeOffset EnqueuedAt { get; } = enqueuedAt;

        public DateTimeOffset LastSeenAt { get; private set; } = enqueuedAt;

        public int Count { get; private set; } = 1;

        public void Increment(LiveDanmakuPriority candidatePriority, DateTimeOffset now)
        {
            Count++;
            LastSeenAt = now;
            if (candidatePriority > Priority)
            {
                Priority = candidatePriority;
            }
        }
    }
}
