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

    private static IReadOnlyList<SponsorBlockSegment> CreateSegments(double start, double end)
        => [new SponsorBlockSegment(SponsorBlockCategory.Sponsor, start, end, "uuid")];
}
