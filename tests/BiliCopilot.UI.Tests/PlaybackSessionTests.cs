// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Player;

namespace BiliCopilot.UI.Tests;

public sealed class PlaybackSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Open_CreatesAndRegistersSessionOnce()
    {
        var harness = new SessionHarness();

        var session = harness.Factory.Open();

        Assert.Equal(1, harness.Created);
        Assert.Same(session.Session, Assert.Single(harness.Registered));
    }

    [Fact]
    public void Open_Twice_CreatesDistinctSessions()
    {
        var harness = new SessionHarness();

        var first = harness.Factory.Open();
        var second = harness.Factory.Open();

        Assert.NotSame(first.Session, second.Session);
        Assert.Equal([first.Session, second.Session], harness.Registered);
        Assert.Equal(2, harness.Created);
    }

    [Fact]
    public void Open_WhenCreationFails_DoesNotRegister()
    {
        var harness = new SessionHarness();
        harness.CreateFailure = new InvalidOperationException("vm");

        Assert.Throws<InvalidOperationException>(() => harness.Factory.Open());

        Assert.Empty(harness.Registered);
        Assert.Empty(harness.Released);
    }

    [Fact]
    public async Task Initialize_Success_MarksSessionReady()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();

        await session.InitializeAsync(() =>
        {
            session.Session.Resources.Add("mpv-client");
            return Task.CompletedTask;
        }).WaitAsync(Timeout);

        Assert.True(session.IsReady);
        Assert.Same(session.Session, Assert.Single(harness.Registered));
        Assert.Empty(harness.Released);
        Assert.Equal(["mpv-client"], session.Session.Resources);
    }

    [Fact]
    public async Task Initialize_Failure_ReleasesCreatedResourcesAndUnregisters()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.InitializeAsync(() =>
        {
            session.Session.Resources.Add("mpv-client");
            throw new InvalidOperationException("mpv");
        })).WaitAsync(Timeout);

        Assert.False(session.IsReady);
        Assert.Empty(session.Session.Resources);
        Assert.Equal([session.Session], harness.Released);
        Assert.Empty(harness.Registered);
    }

    [Fact]
    public async Task Initialize_SecondAttemptFailure_KeepsWorkingSession()
    {
        // 同一窗口切下一集失败时，已初始化的播放器保持原样（现有可见行为）。
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        await session.InitializeAsync(() => Task.CompletedTask).WaitAsync(Timeout);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.InitializeAsync(() => throw new InvalidOperationException("next"))).WaitAsync(Timeout);

        Assert.True(session.IsReady);
        Assert.Single(harness.Registered);
        Assert.Empty(harness.Released);
    }

    [Fact]
    public async Task Initialize_OverlappingFailure_DoesNotRollBackSessionInUse()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = session.InitializeAsync(async () => await running.Task);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.InitializeAsync(() => throw new InvalidOperationException("second"))).WaitAsync(Timeout);
        Assert.Empty(harness.Released);

        running.SetResult();
        await first.WaitAsync(Timeout);
        Assert.True(session.IsReady);
        Assert.Single(harness.Registered);
    }

    [Fact]
    public async Task Initialize_OverlappingCalls_EachAwaitsOnlyItsOwnWork()
    {
        // 关闭/释放仍汇合全部初始化，但单个调用方不被别人的初始化拖住。
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = session.InitializeAsync(async () => await running.Task);

        var second = session.InitializeAsync(() => Task.CompletedTask);

        await second.WaitAsync(Timeout);
        Assert.False(first.IsCompleted);
        running.SetResult();
        await first.WaitAsync(Timeout);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task Release_RunsOnceAndRepeatedCallsJoinSameCompletion()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.ReleaseGate = gate.Task;

        var first = session.ReleaseAsync();
        var second = session.ReleaseAsync();

        Assert.Same(first, second);
        Assert.False(second.IsCompleted);
        gate.SetResult();
        await first.WaitAsync(Timeout);
        Assert.Equal([session.Session], harness.Released);
        Assert.Empty(harness.Registered);
    }

    [Fact]
    public async Task Release_WaitsForRunningInitialization()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initialization = session.InitializeAsync(async () => await running.Task);

        var release = session.ReleaseAsync();
        await Task.Delay(50);
        Assert.False(release.IsCompleted);
        Assert.Empty(harness.Released);

        running.SetResult();
        await release.WaitAsync(Timeout);
        await initialization.WaitAsync(Timeout);
        Assert.Equal([session.Session], harness.Released);
    }

    [Fact]
    public async Task Release_AfterRollback_DoesNotReleaseAgain()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.InitializeAsync(() => throw new InvalidOperationException("mpv"))).WaitAsync(Timeout);

        await session.ReleaseAsync().WaitAsync(Timeout);

        Assert.Equal([session.Session], harness.Released);
        Assert.Empty(harness.Registered);
    }

    [Fact]
    public async Task Release_Failure_StillUnregistersAndSurfaces()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        harness.ReleaseFailure = new InvalidOperationException("dispose");

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ReleaseAsync()).WaitAsync(Timeout);

        Assert.Empty(harness.Registered);
    }

    [Fact]
    public async Task Close_Repeated_JoinsSameCompletionAndRunsWorkOnce()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCalls = 0;

        var first = session.CloseAsync(async () =>
        {
            closeCalls++;
            await gate.Task;
        });
        var second = session.CloseAsync(() =>
        {
            closeCalls++;
            return Task.CompletedTask;
        });

        Assert.Same(first, second);
        Assert.False(second.IsCompleted);
        gate.SetResult();
        await first.WaitAsync(Timeout);
        Assert.Equal(1, closeCalls);
    }

    [Fact]
    public async Task Close_ThenDispose_ReleasesExactlyOnce()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        await session.InitializeAsync(() => Task.CompletedTask).WaitAsync(Timeout);

        await session.CloseAsync(() => session.ReleaseAsync()).WaitAsync(Timeout);
        await session.ReleaseAsync().WaitAsync(Timeout);

        Assert.Equal([session.Session], harness.Released);
        Assert.Empty(harness.Registered);
    }

    [Fact]
    public async Task Initialize_AfterRelease_IsRejected()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        await session.ReleaseAsync().WaitAsync(Timeout);
        var initialized = 0;

        await session.InitializeAsync(() =>
        {
            initialized++;
            return Task.CompletedTask;
        }).WaitAsync(Timeout);

        Assert.Equal(0, initialized);
        Assert.False(session.IsReady);
        Assert.Single(harness.Released);
    }

    [Fact]
    public async Task Initialize_AfterClose_IsRejected()
    {
        var harness = new SessionHarness();
        var session = harness.Factory.Open();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close = session.CloseAsync(() => gate.Task);
        var initialized = 0;

        await session.InitializeAsync(() =>
        {
            initialized++;
            return Task.CompletedTask;
        }).WaitAsync(Timeout);

        gate.SetResult();
        await close.WaitAsync(Timeout);
        Assert.Equal(0, initialized);
    }

    private sealed class SessionHarness
    {
        public SessionHarness()
        {
            Factory = new PlaybackSessionFactory<FakePlayer>(
                _ =>
                {
                    if (CreateFailure is not null)
                    {
                        throw CreateFailure;
                    }

                    Created++;
                    return new FakePlayer();
                },
                player =>
                {
                    if (ReleaseFailure is not null)
                    {
                        throw ReleaseFailure;
                    }

                    player.Resources.Clear();
                    Released.Add(player);
                    return ReleaseGate;
                },
                player => Registered.Add(player),
                player => Registered.Remove(player));
        }

        public int Created { get; private set; }

        public List<FakePlayer> Registered { get; } = [];

        public List<FakePlayer> Released { get; } = [];

        public Exception? CreateFailure { get; set; }

        public Task ReleaseGate { get; set; } = Task.CompletedTask;

        public Exception? ReleaseFailure { get; set; }

        public PlaybackSessionFactory<FakePlayer> Factory { get; }
    }

    private sealed class FakePlayer
    {
        public List<string> Resources { get; } = [];
    }
}
