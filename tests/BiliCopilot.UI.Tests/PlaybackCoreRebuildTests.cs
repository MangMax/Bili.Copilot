// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Player;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 播放核心（MpvClient+MpvPlayer）死亡后的完整重建编排（#228）：
/// 取消未决 → 解绑并释放 player → 释放 client → 重新初始化，单飞且不重复释放。
/// </summary>
public sealed class PlaybackCoreRebuildTests
{
    public PlaybackCoreRebuildTests()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new();
    }

    private static PlaybackCoreRebuildCoordinator NewCoordinator() => new();

    [Fact]
    public async Task RebuildRunsTeardownThenReinitializeInOrder()
    {
        var coordinator = NewCoordinator();
        var steps = new List<string>();

        await coordinator.RebuildAsync(
            cancelPending: () => steps.Add("cancel"),
            disposePlayerAsync: () => { steps.Add("player"); return Task.CompletedTask; },
            disposeClientAsync: () => { steps.Add("client"); return Task.CompletedTask; },
            reinitializeAsync: () => { steps.Add("reinit"); return Task.CompletedTask; });

        Assert.Equal(["cancel", "player", "client", "reinit"], steps);
        Assert.False(coordinator.IsRebuilding);
    }

    [Fact]
    public async Task ConcurrentRebuildsJoinSameRun()
    {
        var coordinator = NewCoordinator();
        var steps = new List<string>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = coordinator.RebuildAsync(
            cancelPending: () => steps.Add("cancel"),
            disposePlayerAsync: () => { steps.Add("player"); return Task.CompletedTask; },
            disposeClientAsync: () => { steps.Add("client"); return gate.Task; },
            reinitializeAsync: () => { steps.Add("reinit"); return Task.CompletedTask; });

        var second = coordinator.RebuildAsync(
            cancelPending: () => steps.Add("cancel2"),
            disposePlayerAsync: () => { steps.Add("player2"); return Task.CompletedTask; },
            disposeClientAsync: () => { steps.Add("client2"); return Task.CompletedTask; },
            reinitializeAsync: () => { steps.Add("reinit2"); return Task.CompletedTask; });

        Assert.True(coordinator.IsRebuilding);
        gate.SetResult();
        await Task.WhenAll(first, second);

        // 并发的重建汇入同一任务：teardown 与重建各只执行一次。
        Assert.Equal(["cancel", "player", "client", "reinit"], steps);
    }

    [Fact]
    public async Task RebuildAfterCompletionRunsAgain()
    {
        var coordinator = NewCoordinator();
        var runs = 0;
        static Task Step() => Task.CompletedTask;

        await coordinator.RebuildAsync(() => { }, Step, Step, () => { runs++; return Task.CompletedTask; });
        await coordinator.RebuildAsync(() => { }, Step, Step, () => { runs++; return Task.CompletedTask; });

        // 核心可能再次 Shutdown：完成后的下一次重建必须重新走完整流程。
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task TeardownFailureDoesNotRunReinitialize()
    {
        var coordinator = NewCoordinator();
        var reinitialized = false;

        var rebuild = coordinator.RebuildAsync(
            cancelPending: () => { },
            disposePlayerAsync: () => Task.CompletedTask,
            disposeClientAsync: () => Task.FromException(new InvalidOperationException("dispose failed")),
            reinitializeAsync: () => { reinitialized = true; return Task.CompletedTask; });

        await Assert.ThrowsAsync<InvalidOperationException>(() => rebuild);
        Assert.False(reinitialized);
        Assert.False(coordinator.IsRebuilding);
    }

    [Fact]
    public async Task DrainedJoinsActiveRebuild()
    {
        var coordinator = NewCoordinator();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = coordinator.RebuildAsync(
            cancelPending: () => { },
            disposePlayerAsync: () => Task.CompletedTask,
            disposeClientAsync: () => Task.CompletedTask,
            reinitializeAsync: () => gate.Task);

        var drained = coordinator.Drained;
        Assert.False(drained.IsCompleted);

        gate.SetResult();
        await drained;
        Assert.True(coordinator.Drained.IsCompleted);
    }

    [Fact]
    public async Task IdleCoordinatorDrainsImmediately()
    {
        var coordinator = NewCoordinator();

        // 没有进行中的重建时 Drained 立即完成：释放路径不会被它拖住。
        Assert.True(coordinator.Drained.IsCompleted);
        Assert.False(coordinator.IsRebuilding);
        await coordinator.Drained;
    }

    [Fact]
    public async Task DeadCore_RefreshCommandRoutesToRebuild()
    {
        var vm = new PlayerViewModel();
        vm.SimulateCoreShutdown();

        Assert.True(vm.RefreshPlaybackCommand.CanExecute(null));
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);

        // 核心已死时刷新按钮就是「重新加载」入口：走完整重建，不做取址刷新。
        Assert.Single(vm.CoreRebuildCalls);
    }

    [Fact]
    public async Task LiveCore_RefreshCommandDoesNotRebuild()
    {
        var vm = new PlayerViewModel();

        // 没有取址器且核心存活：刷新命令不可用，重建入口绝不触发。
        Assert.False(vm.RefreshPlaybackCommand.CanExecute(null));
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);

        Assert.Empty(vm.CoreRebuildCalls);
    }

    [Fact]
    public async Task DisposedClientWithoutShutdown_DoesNotOfferReload()
    {
        var vm = new PlayerViewModel();
        vm.Client!.IsDisposed = true;

        // 客户端已释放但未走 Shutdown（如初始化失败回滚）：
        // 命令依然短路，「重新加载」只面向 Shutdown 死核心。
        Assert.False(vm.RefreshPlaybackCommand.CanExecute(null));
        Assert.Empty(vm.CoreRebuildCalls);
    }
}
