using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

public sealed class LivePlaybackWatchdogTests
{
    [Fact]
    public void StallRequiresEightSecondsAndRecoveryHasSixtySecondCooldown()
    {
        var watchdog = new LivePlaybackWatchdog();
        Assert.False(Sample(watchdog, 0));
        Assert.False(Sample(watchdog, 7.999));
        Assert.True(Sample(watchdog, 8));
        Assert.True(watchdog.TryBeginRecovery(TimeSpan.FromSeconds(8)));
        Assert.False(watchdog.TryBeginRecovery(TimeSpan.FromSeconds(67.999)));
        Assert.True(watchdog.TryBeginRecovery(TimeSpan.FromSeconds(68)));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void PauseTerminalAndActiveRefreshSuppressRecoveryAndRestartStallWindow(bool paused, bool terminal, bool refreshing)
    {
        var watchdog = new LivePlaybackWatchdog();
        Assert.False(Sample(watchdog, 0));
        Assert.False(watchdog.Observe(TimeSpan.FromSeconds(20), 10, 0, paused, terminal, refreshing, ended: true));
        Assert.False(Sample(watchdog, 21));
        Assert.False(Sample(watchdog, 28.999));
        Assert.True(Sample(watchdog, 29));
    }

    [Fact]
    public void ProgressAndHealthyCacheResetStallObservation()
    {
        var watchdog = new LivePlaybackWatchdog();
        Assert.False(Sample(watchdog, 0));
        Assert.False(watchdog.Observe(TimeSpan.FromSeconds(7), 10.5, 0, false, false, false, false));
        Assert.False(watchdog.Observe(TimeSpan.FromSeconds(14), 10.5, 51200, false, false, false, false));
        Assert.False(Sample(watchdog, 15));
        Assert.True(Sample(watchdog, 23));
    }

    [Fact]
    public void ThreeFailuresRequireManualResetAndSessionResetClearsCooldown()
    {
        var watchdog = new LivePlaybackWatchdog();
        for (var i = 0; i < 3; i++)
        {
            Assert.True(watchdog.TryBeginRecovery(TimeSpan.FromSeconds(i * 60)));
            Assert.Equal(i < 2, watchdog.RecordFailure());
        }

        Assert.False(watchdog.TryBeginRecovery(TimeSpan.FromSeconds(180)));
        watchdog.ResetFailures();
        Assert.True(watchdog.TryBeginRecovery(TimeSpan.FromSeconds(180)));
        watchdog.Reset();
        Assert.True(watchdog.TryBeginRecovery(TimeSpan.Zero));
    }

    [Fact]
    public void EofIsRecoverableWithoutWaitingForPositionStall()
    {
        var watchdog = new LivePlaybackWatchdog();
        Assert.True(watchdog.Observe(TimeSpan.Zero, 10, 100000, false, false, false, true));
        Assert.True(watchdog.TryBeginRecovery(TimeSpan.Zero));
        Assert.False(watchdog.TryBeginRecovery(TimeSpan.FromSeconds(59)));
    }

    private static bool Sample(LivePlaybackWatchdog watchdog, double seconds)
        => watchdog.Observe(TimeSpan.FromSeconds(seconds), 10, 0, false, false, false, false);
}
