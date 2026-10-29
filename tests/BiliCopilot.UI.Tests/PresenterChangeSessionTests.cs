// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class PresenterChangeSessionTests
{
    [Fact]
    public async Task LatestCompletionThenOlderCompletionCannotSettleThirdChange()
    {
        var delays = Enumerable.Range(0, 3).Select(_ => new TaskCompletionSource()).ToArray();
        var queue = new Queue<Task>(delays.Select(d => d.Task));
        var session = new PresenterChangeSession(() => queue.Dequeue());
        var visible = "Overlapped";
        var first = session.ChangeAsync(() => visible = "FullScreen");
        var second = session.ChangeAsync(() => visible = "CompactOverlay");
        delays[1].SetResult();
        await second;
        Assert.False(session.IsChanging);
        var third = session.ChangeAsync(() => visible = "Overlapped");
        delays[0].SetResult();
        await first;
        Assert.True(session.IsChanging);
        Assert.Equal("Overlapped", visible);
        delays[2].SetResult();
        await third;
        Assert.False(session.IsChanging);
        Assert.Equal("Overlapped", visible);
    }

    [Fact]
    public async Task PresenterCallbackCanCloseSessionBeforeDelayCompletes()
    {
        var pending = new TaskCompletionSource();
        var session = new PresenterChangeSession(() => pending.Task);
        var operation = session.ChangeAsync(session.Close);
        Assert.False(session.IsChanging);
        pending.SetResult();
        await operation;
        await session.ChangeAsync(() => throw new InvalidOperationException("Closed window accessed"));
        Assert.False(session.IsChanging);
    }

    [Fact]
    public async Task ImmediateCompletionAppliesPresenterWhileChangingAndSettles()
    {
        var session = new PresenterChangeSession(() => Task.CompletedTask);
        Assert.False(session.IsChanging);
        var visible = "Overlapped";
        await session.ChangeAsync(() =>
        {
            Assert.True(session.IsChanging);
            visible = "FullScreen";
        });
        Assert.False(session.IsChanging);
        Assert.Equal("FullScreen", visible);
    }

    [Fact]
    public void PresenterFailurePropagatesSynchronouslyAndDoesNotLeaveChangingState()
    {
        var session = new PresenterChangeSession(() => Task.CompletedTask);
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = session.ChangeAsync(() => throw new InvalidOperationException("Presenter rejected"));
        });
        Assert.False(session.IsChanging);
    }

    [Fact]
    public async Task CloseRejectsFurtherWindowAccessAndLateCompletion()
    {
        var pending = new TaskCompletionSource();
        var session = new PresenterChangeSession(() => pending.Task);
        var visible = "Overlapped";
        var switching = session.ChangeAsync(() => visible = "FullScreen");
        session.Close();
        session.Close();
        await session.ChangeAsync(() => throw new InvalidOperationException("Destroyed window accessed"));
        Assert.False(session.IsChanging);
        pending.SetResult();
        await switching;
        Assert.False(session.IsChanging);
        Assert.Equal("FullScreen", visible);
    }

    [Fact]
    public async Task OldCompletionCannotSettleNewPresenter()
    {
        var firstDelay = new TaskCompletionSource();
        var secondDelay = new TaskCompletionSource();
        var delays = new Queue<Task>([firstDelay.Task, secondDelay.Task]);
        var session = new PresenterChangeSession(() => delays.Dequeue());
        var visible = "Overlapped";

        var first = session.ChangeAsync(() => visible = "FullScreen");
        Assert.Equal("FullScreen", visible);
        Assert.False(first.IsCompleted);
        var second = session.ChangeAsync(() => visible = "CompactOverlay");
        firstDelay.SetResult();
        await first;

        Assert.True(session.IsChanging);
        Assert.Equal("CompactOverlay", visible);
        Assert.False(second.IsCompleted);
        secondDelay.SetResult();
        await second;
        Assert.False(session.IsChanging);
        Assert.Equal("CompactOverlay", visible);
    }
}
