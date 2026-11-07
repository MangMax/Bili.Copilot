// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播聊天的有界缓冲：容量满时整批丢弃最旧的条目，不做逐条头部删除。
/// </summary>
/// <remarks>
/// 内部用"头偏移 + 列表"实现环形语义：追加只写到尾部，溢出时一次性推进头偏移并整批计数，
/// 头偏移超过容量时再压缩一次，因此洪峰下没有 <c>RemoveAt(0)</c> 循环，也没有每消息分配。
/// 醒目留言按标识单独移除（<see cref="RemoveSuperChat"/>），供 SUPER_CHAT_MESSAGE_DELETE 使用。
/// </remarks>
public sealed class LiveChatBuffer
{
    /// <summary>
    /// 默认容量：约 500 条。
    /// </summary>
    public const int DefaultCapacity = 500;

    private readonly List<LiveChatMessage> _items;
    private readonly int _capacity;
    private int _head;
    private long _totalAppended;
    private long _totalTrimmed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatBuffer"/> class.
    /// </summary>
    /// <param name="capacity">容量上限.</param>
    public LiveChatBuffer(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "聊天缓冲容量必须大于零。");
        }

        _capacity = capacity;
        _items = new List<LiveChatMessage>(Math.Min(capacity, 1024));
    }

    /// <summary>
    /// Gets 容量上限.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets 当前条数.
    /// </summary>
    public int Count => _items.Count - _head;

    /// <summary>
    /// Gets 会话内累计追加条数.
    /// </summary>
    public long TotalAppended => Interlocked.Read(ref _totalAppended);

    /// <summary>
    /// Gets 因容量上限被裁剪的条数.
    /// </summary>
    public long TotalTrimmed => Interlocked.Read(ref _totalTrimmed);

    /// <summary>
    /// Gets 当前可见条目（按时间顺序）.
    /// </summary>
    public IReadOnlyList<LiveChatMessage> Items
    {
        get
        {
            if (_head == 0)
            {
                return _items.ToArray();
            }

            var result = new LiveChatMessage[_items.Count - _head];
            _items.CopyTo(_head, result, 0, result.Length);
            return result;
        }
    }

    /// <summary>
    /// 追加一条消息。
    /// </summary>
    /// <param name="message">消息.</param>
    /// <returns>本次因容量上限被丢弃的条数；未溢出时为 0.</returns>
    public int Append(LiveChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _items.Add(message);
        Interlocked.Increment(ref _totalAppended);

        var overflow = _items.Count - _head - _capacity;
        if (overflow <= 0)
        {
            return 0;
        }

        // 整批推进头偏移，而不是逐条 RemoveAt(0)。
        _head += overflow;
        Interlocked.Add(ref _totalTrimmed, overflow);
        if (_head >= _capacity)
        {
            _items.RemoveRange(0, _head);
            _head = 0;
        }

        return overflow;
    }

    /// <summary>
    /// 按标识移除醒目留言。
    /// </summary>
    /// <param name="superChatIds">要移除的醒目留言标识.</param>
    /// <returns>实际移除的条数.</returns>
    public int RemoveSuperChat(IReadOnlyList<string>? superChatIds)
    {
        if (superChatIds is null || superChatIds.Count == 0 || Count == 0)
        {
            return 0;
        }

        var ids = new HashSet<string>(superChatIds.Where(p => !string.IsNullOrEmpty(p)), StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        for (var i = _items.Count - 1; i >= _head; i--)
        {
            var item = _items[i];
            if (item.IsSuperChat && item.SuperChatId is { } id && ids.Contains(id))
            {
                _items.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// 清空消息；累计计数保留，便于诊断。
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        _head = 0;
    }
}
