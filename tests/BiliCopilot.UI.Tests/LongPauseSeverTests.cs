// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Player;
using Richasy.MpvKernel.Core.Enums;

namespace BiliCopilot.UI.Tests;

public class LongPauseSeverTests
{
    private sealed class FakeTimer : ILongPauseSeverTimer
    {
        public int CancelCount { get; private set; }
        public void Cancel() => CancelCount++;
    }

    private sealed class Fixture
    {
        public List<FakeTimer> Timers { get; } = [];
        public List<double> Severs { get; } = [];
        public TimeSpan LastDelay { get; private set; }

        public LongPauseSeverMonitor CreateMonitor(TimeSpan? threshold = null)
            => new(threshold ?? LongPauseSeverPolicy.Threshold, Schedule, pos => Severs.Add(pos));

        private ILongPauseSeverTimer Schedule(TimeSpan delay)
        {
            LastDelay = delay;
            var timer = new FakeTimer();
            Timers.Add(timer);
            return timer;
        }

        public bool Fire(LongPauseSeverMonitor monitor, double position)
            => monitor.Sever(position);
    }

    private static Fixture NewFixture() => new();

    [Fact]
    public void PausedStartsTimerWithThreshold()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        Assert.Single(fixture.Timers);
        Assert.Equal(LongPauseSeverPolicy.Threshold, fixture.LastDelay);
        Assert.True(monitor.IsPending);
    }

    [Fact]
    public void ThresholdElapsedWhilePausedSeversAndRecordsPosition()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        var severed = fixture.Fire(monitor, 1234.5);
        Assert.True(severed);
        Assert.Equal([1234.5], fixture.Severs);
        Assert.Equal(1234.5, monitor.PendingResumePosition);
        Assert.True(monitor.IsSeveredByLongPause);
        Assert.False(monitor.IsPending);
    }

    [Theory]
    [InlineData(MpvPlayerState.Playing)]
    [InlineData(MpvPlayerState.Seeking)]
    [InlineData(MpvPlayerState.End)]
    public void StateChangeAwayFromPausedCancelsTimer(MpvPlayerState state)
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        monitor.OnPlaybackStateChanged(state, isLive: false);
        Assert.Equal(1, fixture.Timers[0].CancelCount);
        Assert.False(monitor.IsPending);
    }

    [Fact]
    public void SeverAfterCancelDoesNothing()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        monitor.CancelTimer();
        Assert.False(fixture.Fire(monitor, 10));
        Assert.Empty(fixture.Severs);
        Assert.Null(monitor.PendingResumePosition);
        Assert.False(monitor.IsSeveredByLongPause);
    }

    [Fact]
    public void ReEnteringPausedRestartsTimer()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        monitor.OnPlaybackStateChanged(MpvPlayerState.Playing, isLive: false);
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        Assert.Equal(2, fixture.Timers.Count);
        Assert.Equal(1, fixture.Timers[0].CancelCount);
        Assert.Equal(0, fixture.Timers[1].CancelCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LiveExemptionSkipsTimer(bool isLive)
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive);
        if (isLive)
        {
            Assert.Empty(fixture.Timers);
            Assert.False(monitor.IsPending);
        }
        else
        {
            Assert.Single(fixture.Timers);
        }
    }

    [Fact]
    public void LiveExemptionCancelsExistingTimer()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: true);
        Assert.Equal(1, fixture.Timers[0].CancelCount);
        Assert.False(monitor.IsPending);
    }

    [Fact]
    public void ResetClearsPendingResumeState()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        fixture.Fire(monitor, 42);
        monitor.Reset();
        Assert.Null(monitor.PendingResumePosition);
        Assert.False(monitor.IsSeveredByLongPause);
    }

    [Fact]
    public void ClearPendingResumeConsumedByNextTicket()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        fixture.Fire(monitor, 88);
        monitor.ClearPendingResume();
        Assert.Null(monitor.PendingResumePosition);
        Assert.False(monitor.IsSeveredByLongPause);
    }

    [Fact]
    public void SeveredStateSurvivesCancelTimer()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        fixture.Fire(monitor, 7);
        monitor.CancelTimer();
        Assert.True(monitor.IsSeveredByLongPause);
        Assert.Equal(7, monitor.PendingResumePosition);
    }

    [Fact]
    public void DefaultThresholdIsMinutesScale()
    {
        Assert.True(LongPauseSeverPolicy.Threshold >= TimeSpan.FromMinutes(1));
        Assert.True(LongPauseSeverPolicy.Threshold <= TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void LateDuplicateSeverIgnored()
    {
        var fixture = NewFixture();
        var monitor = fixture.CreateMonitor();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        Assert.True(fixture.Fire(monitor, 1));
        Assert.False(fixture.Fire(monitor, 2));
        Assert.Equal([1], fixture.Severs);
    }
}
