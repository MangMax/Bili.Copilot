// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手片段缓存，以「BV 号 + 分 P cid」为键，避免不同分 P 串用片段.
/// </summary>
public sealed class SponsorBlockSegmentCache
{
    private const int DefaultCapacity = 200;
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly Queue<string> _insertionOrder = new();
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
                _ = _entries.Remove(key);
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
                while (_entries.Count >= _capacity && _insertionOrder.Count > 0)
                {
                    var oldest = _insertionOrder.Dequeue();
                    _ = _entries.Remove(oldest);
                }

                _insertionOrder.Enqueue(key);
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
        }
    }

    private static string CreateKey(string bvid, string cid)
        => $"{bvid}\u001f{cid}";

    private readonly record struct CacheEntry(IReadOnlyList<SponsorBlockSegment> Segments, DateTimeOffset CreatedAt);
}
