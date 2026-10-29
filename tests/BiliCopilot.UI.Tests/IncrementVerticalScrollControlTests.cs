using BiliCopilot.UI.Controls;
using CommunityToolkit.Mvvm.Input;

namespace BiliCopilot.UI.Tests;

public sealed class IncrementVerticalScrollControlTests
{
    [Fact]
    public void RepeatedLoadedEventsKeepOneSubscriptionAndUnloadRemovesIt()
    {
        var control = new IncrementVerticalScrollControl();
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() => loads++);
        for (var cycle = 0; cycle < 3; cycle++)
        {
            control.Load();
            control.Load();
            Assert.Equal(1, control.View.ViewChangedSubscriptions);
            Assert.Equal(1, control.View.SizeChangedSubscriptions);
            Assert.Equal(1, control.DispatcherQueue.PendingCount);
            control.DispatcherQueue.Drain();
            Assert.Equal(cycle + 1, loads);
            control.Unload();
            control.Unload();
            Assert.Equal(0, control.View.ViewChangedSubscriptions);
            Assert.Equal(0, control.View.SizeChangedSubscriptions);
            control.View.ChangeView();
            control.View.Resize();
            Assert.Equal(0, control.DispatcherQueue.PendingCount);
        }
    }

    [Fact]
    public async Task ThreeRemountsRejectOldDelaysAndAcceptCurrentDelay()
    {
        var clock = new ManualClock();
        var control = new IncrementVerticalScrollControl(clock);
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() => loads++);
        control.View.ExtentHeight = 5000;
        control.Load();
        control.DispatcherQueue.Drain();
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var oldCheck = control.DelayCheckItemsAsync();
            control.Unload();
            control.Load();
            control.DispatcherQueue.Drain();
            control.View.ExtentHeight = 300;
            clock.Advance(500);
            await oldCheck;
            Assert.Equal(0, control.DispatcherQueue.PendingCount);
            Assert.Equal(cycle, loads);

            var currentCheck = control.DelayCheckItemsAsync();
            clock.Advance(500);
            await currentCheck;
            Assert.Equal(1, control.DispatcherQueue.PendingCount);
            control.DispatcherQueue.Drain();
            Assert.Equal(cycle + 1, loads);
            control.View.ExtentHeight = 5000;
        }
    }

    [Theory]
    [InlineData(799, 1)]
    [InlineData(800, 1)]
    [InlineData(801, 0)]
    public void QueuedCheckUsesLatestPositionAndThreshold(double remaining, int expectedLoads)
    {
        var control = new IncrementVerticalScrollControl();
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() => loads++);
        control.Load(); // Initially short; move away before the callback runs.
        control.View.ExtentHeight = 3000;
        control.View.ViewportHeight = 600;
        control.View.VerticalOffset = 2400 - remaining;
        control.DispatcherQueue.Drain();
        Assert.Equal(expectedLoads, loads);
    }

    [Fact]
    public void ReloadBeforeQueueDrainsStillHasOneCurrentCheck()
    {
        var control = new IncrementVerticalScrollControl();
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() => loads++);
        control.Load();
        control.Unload();
        control.Load();
        control.View.Resize();
        Assert.Equal(1, control.DispatcherQueue.PendingCount);
        control.DispatcherQueue.Drain();
        Assert.Equal(1, loads);
        control.Unload();
        control.View.ChangeView();
        control.View.Resize();
        Assert.Equal(0, control.DispatcherQueue.PendingCount);
    }

    [Fact]
    public async Task ShortPagesFillViewportThenScrollAndResizeContinueUntilLastPage()
    {
        var clock = new ManualClock();
        var control = new IncrementVerticalScrollControl(clock);
        var pages = new List<int>();
        var canLoad = true;
        control.LoadMoreCommand = new RelayCommand(() =>
        {
            pages.Add(pages.Count + 1);
            canLoad = false; // An in-flight page cannot be requested twice.
        }, () => canLoad);
        control.Load();
        control.DispatcherQueue.Drain();
        Assert.Equal(new[] { 1 }, pages);

        async Task CompletePage(double extent, bool hasMore)
        {
            control.View.ExtentHeight = extent;
            var check = control.DelayCheckItemsAsync(); // Existing list-updated boundary.
            canLoad = hasMore;
            clock.Advance(499);
            Assert.False(check.IsCompleted);
            Assert.Equal(0, control.DispatcherQueue.PendingCount);
            clock.Advance(1);
            await check;
            control.DispatcherQueue.Drain();
        }

        await CompletePage(300, true);
        Assert.Equal(new[] { 1, 2 }, pages);
        await CompletePage(500, true);
        Assert.Equal(new[] { 1, 2, 3 }, pages);
        await CompletePage(2200, true);
        Assert.Equal(new[] { 1, 2, 3 }, pages);

        for (var i = 0; i < 100; i++)
        {
            control.View.VerticalOffset = i * 10;
            control.View.ChangeView();
        }
        control.DispatcherQueue.Drain();
        Assert.Equal(new[] { 1, 2, 3, 4 }, pages);
        // More movement while the request is in flight must not execute it again.
        control.View.ChangeView();
        control.DispatcherQueue.Drain();
        Assert.Equal(4, pages.Count);
        await CompletePage(3200, true);
        Assert.Equal(4, pages.Count);

        control.View.ViewportHeight = 1500;
        control.View.Resize();
        control.DispatcherQueue.Drain();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, pages);
        await CompletePage(3300, false);
        for (var i = 0; i < 100; i++) control.View.Resize();
        control.DispatcherQueue.Drain();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, pages);
        Assert.Equal(0, control.DispatcherQueue.PendingCount);
    }

    [Fact]
    public async Task DelayedChecksCoalesceAndCannotLoadWhileUnloaded()
    {
        var clock = new ManualClock();
        var control = new IncrementVerticalScrollControl(clock);
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() => loads++);
        control.View.ExtentHeight = 5000;
        control.Load();
        control.DispatcherQueue.Drain();
        var checks = Enumerable.Range(0, 20).Select(_ => control.DelayCheckItemsAsync()).ToArray();
        control.View.ExtentHeight = 100;
        clock.Advance(500);
        await Task.WhenAll(checks);
        Assert.Equal(1, control.DispatcherQueue.PendingCount);
        control.DispatcherQueue.Drain();
        Assert.Equal(1, loads);

        var pending = control.DelayCheckItemsAsync();
        control.Unload();
        clock.Advance(500);
        await pending;
        await control.DelayCheckItemsAsync();
        control.DispatcherQueue.Drain();
        Assert.Equal(1, loads);
        Assert.Equal(0, control.DispatcherQueue.PendingCount);
    }

    [Fact]
    public void RejectedEnqueueCanRetryAndCommandIsReadAtExecution()
    {
        var control = new IncrementVerticalScrollControl();
        control.DispatcherQueue.Accept = false;
        control.Load();
        Assert.Equal(0, control.DispatcherQueue.PendingCount);
        control.DispatcherQueue.Accept = true;
        control.View.Resize();
        control.DispatcherQueue.Drain(); // Null command is harmless.
        var oldLoads = 0;
        var newLoads = 0;
        control.LoadMoreCommand = new RelayCommand(() => oldLoads++);
        control.View.ChangeView();
        control.LoadMoreCommand = new RelayCommand(() => newLoads++);
        control.DispatcherQueue.Drain();
        Assert.Equal(0, oldLoads);
        Assert.Equal(1, newLoads);
    }

    [Fact]
    public void ReentrantLayoutKeepsOneFollowUpCheckWithoutLosingNextPage()
    {
        var control = new IncrementVerticalScrollControl();
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() =>
        {
            loads++;
            if (loads == 2) control.View.ExtentHeight = 5000;
            control.View.ChangeView();
            control.View.Resize();
        });
        control.Load();
        control.DispatcherQueue.RunNext();
        Assert.Equal(1, control.DispatcherQueue.PendingCount);
        control.DispatcherQueue.Drain();
        Assert.Equal(2, loads);
        Assert.Equal(0, control.DispatcherQueue.PendingCount);
    }

    [Fact]
    public async Task OldDelayedCheckCannotLoadIntoReloadedControl()
    {
        var clock = new ManualClock();
        var control = new IncrementVerticalScrollControl(clock);
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() => loads++);
        control.View.ExtentHeight = 5000;
        control.Load();
        control.DispatcherQueue.Drain();
        var oldCheck = control.DelayCheckItemsAsync();
        control.Unload();
        control.Load();
        control.DispatcherQueue.Drain();
        control.View.ExtentHeight = 300;
        clock.Advance(500);
        await oldCheck;
        control.DispatcherQueue.Drain();
        Assert.Equal(0, loads);

        var currentCheck = control.DelayCheckItemsAsync();
        clock.Advance(500);
        await currentCheck;
        control.DispatcherQueue.Drain();
        Assert.Equal(1, loads);
    }

    [Fact]
    public void DisabledCommandIsNotExecutedAndLaterInputCanRetry()
    {
        var control = new IncrementVerticalScrollControl();
        var loads = 0;
        var enabled = false;
        control.LoadMoreCommand = new RelayCommand(() => loads++, () => enabled);
        control.Load();
        control.DispatcherQueue.Drain();
        Assert.Equal(0, loads);
        enabled = true;
        control.View.ChangeView();
        control.DispatcherQueue.Drain();
        Assert.Equal(1, loads);
    }

    [Fact]
    public void UnloadDiscardsQueuedLoadAndReloadChecksCurrentView()
    {
        var control = new IncrementVerticalScrollControl();
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() => loads++);
        control.Load();
        control.View.ChangeView();
        control.Unload();
        control.DispatcherQueue.Drain();
        Assert.Equal(0, loads);

        control.Load();
        control.DispatcherQueue.Drain();
        Assert.Equal(1, loads);
    }

    [Fact]
    public void BurstQueuesOneCheckAndExecutesLoadMoreOnce()
    {
        var control = new IncrementVerticalScrollControl();
        var loads = 0;
        control.LoadMoreCommand = new RelayCommand(() => loads++);
        control.Load();
        for (var i = 0; i < 100; i++)
        {
            control.View.ChangeView();
            control.View.Resize();
        }

        Assert.Equal(1, control.DispatcherQueue.PendingCount);
        Assert.Equal(0, loads);
        control.DispatcherQueue.Drain();
        Assert.Equal(1, loads);
    }
    // Controlled time is local to this control's tests, not shared test infrastructure.
    private sealed class ManualClock : TimeProvider
    {
        private long _milliseconds;
        private readonly List<Timer> _timers = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(this, callback, state);
            _timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(int milliseconds)
        {
            _milliseconds += milliseconds;
            foreach (var timer in _timers.ToArray()) timer.FireIfDue();
        }

        private sealed class Timer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long _due = long.MaxValue;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Assert.Equal(Timeout.InfiniteTimeSpan, period);
                _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._milliseconds + (long)dueTime.TotalMilliseconds;
                return true;
            }

            public void FireIfDue()
            {
                if (_due > clock._milliseconds) return;
                _due = long.MaxValue;
                callback(state);
            }

            public void Dispose() => _due = long.MaxValue;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
