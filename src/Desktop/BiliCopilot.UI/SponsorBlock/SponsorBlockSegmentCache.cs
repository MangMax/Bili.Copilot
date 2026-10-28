// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手片段缓存，以「BV 号 + 分 P cid」为键，避免不同分 P 串用片段.
/// </summary>
public sealed class SponsorBlockSegmentCache
{
    private const int DefaultCapacity = 200;
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _insertionOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _orderNodes = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly TimeSpan _timeToLive;
    private readonly int _capacity;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockSegmentCache"/> class.
    /// </summary>
    /// <param name="timeToLive">缓存有效期，默认五分钟.</param>
    /// <param name="capacity">最大缓存条目数.</param>
    public SponsorBlockSegmentCache(TimeSpan? timeToLive = null, int capacity = DefaultCapacity)
    {
        _timeToLive = timeToLive ?? TimeSpan.FromMinutes(5);
        _capacity = Math.Max(1, capacity);
    }

    /// <summary>
    /// 尝试读取仍然有效的缓存.
    /// </summary>
    /// <param name="bvid">视频 BV 号.</param>
    /// <param name="cid">分 P cid.</param>
    /// <param name="now">当前时间.</param>
    /// <param name="segments">命中的片段列表.</param>
    /// <returns>命中有效缓存时返回 <c>true</c>.</returns>
    public bool TryGet(string bvid, string cid, DateTimeOffset now, out IReadOnlyList<SponsorBlockSegment> segments)
    {
        segments = Array.Empty<SponsorBlockSegment>();
        var key = CreateKey(bvid, cid);

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                return false;
            }

            if (now - entry.CreatedAt >= _timeToLive)
            {
                // 过期条目必须同时从插入队列移除，否则重新写入会产生重复队列项，
                // 后续淘汰可能按重复项删掉仍然有效的新条目.
                _ = _entries.Remove(key);
                RemoveFromOrder(key);
                return false;
            }

            segments = entry.Segments;
            return true;
        }
    }

    /// <summary>
    /// 写入缓存.
    /// </summary>
    /// <param name="bvid">视频 BV 号.</param>
    /// <param name="cid">分 P cid.</param>
    /// <param name="segments">片段列表.</param>
    /// <param name="now">当前时间.</param>
    public void Set(string bvid, string cid, IReadOnlyList<SponsorBlockSegment> segments, DateTimeOffset now)
    {
        var key = CreateKey(bvid, cid);

        lock (_lock)
        {
            if (!_entries.ContainsKey(key))
            {
                while (_entries.Count >= _capacity && _insertionOrder.First is { } oldest)
                {
                    _insertionOrder.RemoveFirst();
                    _ = _orderNodes.Remove(oldest.Value);
                    _ = _entries.Remove(oldest.Value);
                }

                _orderNodes[key] = _insertionOrder.AddLast(key);
            }

            _entries[key] = new CacheEntry(segments, now);
        }
    }

    /// <summary>
    /// 清空缓存.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _insertionOrder.Clear();
            _orderNodes.Clear();
        }
    }

    private static string CreateKey(string bvid, string cid)
        => $"{bvid}\u001f{cid}";

    private void RemoveFromOrder(string key)
    {
        if (_orderNodes.Remove(key, out var node))
        {
            _insertionOrder.Remove(node);
        }
    }

    private readonly record struct CacheEntry(IReadOnlyList<SponsorBlockSegment> Segments, DateTimeOffset CreatedAt);
}
