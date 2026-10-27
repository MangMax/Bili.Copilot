// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockSkipTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryTakeSkip_InsideSegment_ReturnsEndAndMarksSkipped()
    {
        var tracker = new SponsorBlockSkipTracker();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        Assert.True(tracker.TryTakeSkip([segment], 100, 900, Start, out var matched, out var target));
        Assert.Same(segment, matched);
        Assert.Equal(110, target);

        Assert.False(tracker.TryTakeSkip([segment], 100, 900, Start.AddMilliseconds(300), out _, out _));
    }

    [Fact]
    public void TryTakeSkip_BeforeTolerance_DoesNotSkip()
    {
        var tracker = new SponsorBlockSkipTracker();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Intro, 90, 110, "a");

        Assert.False(tracker.TryTakeSkip([segment], 89.4, 900, Start, out _, out _));
    }

    [Fact]
    public void TryTakeSkip_WithinStartTolerance_Skips()
    {
        var tracker = new SponsorBlockSkipTracker();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Intro, 90, 110, "a");

        Assert.True(tracker.TryTakeSkip([segment], 89.7, 900, Start, out _, out var target));
        Assert.Equal(110, target);
    }

    [Fact]
    public void TryTakeSkip_AtOrAfterEnd_DoesNotSkip()
    {
        var tracker = new SponsorBlockSkipTracker();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        Assert.False(tracker.TryTakeSkip([segment], 110, 900, Start, out _, out _));
        Assert.False(tracker.TryTakeSkip([segment], 120, 900, Start.AddSeconds(1), out _, out _));
    }

    [Fact]
    public void TryTakeSkip_ThrottlesRapidChecks()
    {
        var tracker = new SponsorBlockSkipTracker();
        SponsorBlockSegment[] segments =
        [
            new(SponsorBlockCategory.Sponsor, 90, 110, "a"),
            new(SponsorBlockCategory.Outro, 300, 320, "b"),
        ];

        Assert.True(tracker.TryTakeSkip(segments, 100, 900, Start, out _, out _));
        Assert.False(tracker.TryTakeSkip(segments, 300, 900, Start.AddMilliseconds(100), out _, out _));
        Assert.True(tracker.TryTakeSkip(segments, 300, 900, Start.AddMilliseconds(300), out var matched, out var target));
        Assert.Equal("b", matched?.Uuid);
        Assert.Equal(320, target);
    }

    [Fact]
    public void TryTakeSkip_ClampsTargetToDuration()
    {
        var tracker = new SponsorBlockSkipTracker();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Outro, 90, 200, "a");

        Assert.True(tracker.TryTakeSkip([segment], 100, 150, Start, out _, out var target));
        Assert.Equal(150, target);
    }

    [Fact]
    public void TryTakeSkip_TooLittleRemaining_DoesNotSkip()
    {
        var tracker = new SponsorBlockSkipTracker();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Outro, 90, 100.2, "a");

        Assert.False(tracker.TryTakeSkip([segment], 100, 900, Start, out _, out _));
    }

    [Fact]
    public void Reset_ClearsSkippedHistory()
    {
        var tracker = new SponsorBlockSkipTracker();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        Assert.True(tracker.TryTakeSkip([segment], 100, 900, Start, out _, out _));
        tracker.Reset();

        Assert.True(tracker.TryTakeSkip([segment], 100, 900, Start.AddMinutes(1), out _, out _));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void TryTakeSkip_InvalidPosition_DoesNotSkip(double position)
    {
        var tracker = new SponsorBlockSkipTracker();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        Assert.False(tracker.TryTakeSkip([segment], position, 900, Start, out _, out _));
    }

    [Fact]
    public void TryTakeSkip_NoSegments_DoesNotSkip()
    {
        var tracker = new SponsorBlockSkipTracker();

        Assert.False(tracker.TryTakeSkip([], 100, 900, Start, out _, out _));
    }
}
