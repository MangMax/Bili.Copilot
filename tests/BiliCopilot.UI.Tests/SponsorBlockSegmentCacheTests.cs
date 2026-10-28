// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockSegmentCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryGet_BeforeSet_ReturnsFalse()
    {
        var cache = new SponsorBlockSegmentCache();

        Assert.False(cache.TryGet("BV1xx", "100", Now, out var segments));
        Assert.Empty(segments);
    }

    [Fact]
    public void Set_ThenTryGetWithinFiveMinutes_ReturnsSameSegments()
    {
        var cache = new SponsorBlockSegmentCache();
        var expected = CreateSegments(10, 20);

        cache.Set("BV1xx", "100", expected, Now);

        Assert.True(cache.TryGet("BV1xx", "100", Now.AddMinutes(4), out var segments));
        Assert.Same(expected, segments);
    }

    [Fact]
    public void TryGet_AfterFiveMinutes_ReturnsFalse()
    {
        var cache = new SponsorBlockSegmentCache();
        cache.Set("BV1xx", "100", CreateSegments(10, 20), Now);

        Assert.False(cache.TryGet("BV1xx", "100", Now.AddSeconds(301), out _));
    }

    [Fact]
    public void TryGet_DifferentPart_DoesNotReuseSegments()
    {
        var cache = new SponsorBlockSegmentCache();
        cache.Set("BV1xx", "100", CreateSegments(10, 20), Now);

        Assert.False(cache.TryGet("BV1xx", "200", Now, out _));
        Assert.False(cache.TryGet("BV1yy", "100", Now, out _));
    }

    [Fact]
    public void Set_WhenCapacityReached_EvictsOldestEntry()
    {
        var cache = new SponsorBlockSegmentCache(capacity: 2);
        cache.Set("BV1a", "1", CreateSegments(1, 2), Now);
        cache.Set("BV1b", "1", CreateSegments(3, 4), Now);
        cache.Set("BV1c", "1", CreateSegments(5, 6), Now);

        Assert.False(cache.TryGet("BV1a", "1", Now, out _));
        Assert.True(cache.TryGet("BV1b", "1", Now, out _));
        Assert.True(cache.TryGet("BV1c", "1", Now, out _));
    }

    [Fact]
    public void TryGetExpired_ThenReinsert_EvictionKeepsLiveEntries()
    {
        // 过期条目必须同时从插入队列中移除；否则重新写入会产生重复队列项，
        // 后续淘汰会按重复项把仍然有效的新条目删掉.
        var cache = new SponsorBlockSegmentCache(capacity: 2);
        var expired = CreateSegments(1, 2);
        var survivor = CreateSegments(3, 4);
        var added = CreateSegments(5, 6);
        var later = Now.AddMinutes(6);

        cache.Set("BV1a", "1", expired, Now);
        cache.Set("BV1b", "1", survivor, Now);
        Assert.False(cache.TryGet("BV1a", "1", later, out _));

        cache.Set("BV1a", "1", expired, later);
        cache.Set("BV1c", "1", added, later);

        Assert.True(cache.TryGet("BV1a", "1", later, out var reinserted));
        Assert.Same(expired, reinserted);
        Assert.True(cache.TryGet("BV1c", "1", later, out _));

        // 最老的存活条目被淘汰.
        Assert.False(cache.TryGet("BV1b", "1", later, out _));
    }

    private static IReadOnlyList<SponsorBlockSegment> CreateSegments(double start, double end)
        => [new SponsorBlockSegment(SponsorBlockCategory.Sponsor, start, end, "uuid")];
}
