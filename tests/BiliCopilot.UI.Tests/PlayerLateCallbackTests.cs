// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 关闭/换集/替换实例之后到达的回调不得再操作旧播放器。
/// </summary>
public class PlayerLateCallbackTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public PlayerLateCallbackTests() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = new();

    public void Dispose() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = null;

    [Fact]
    public void QueuedSmtcCallbackRunsWhileSessionIsCurrent()
    {
        var vm = new PlayerViewModel();
        var isCurrent = vm.CapturePlaybackValidity();

        vm.RunSmtcAction(SmtcPlayerAction.PlayPause, isCurrent);

        Assert.Equal([SmtcPlayerAction.PlayPause], vm.SmtcActions);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("close")]
    [InlineData("player")]
    [InlineData("client")]
    public void QueuedSmtcCallbackDoesNotRunAfterSessionInvalidated(string boundary)
    {
        var vm = new PlayerViewModel();
        var isCurrent = vm.CapturePlaybackValidity();
        Invalidate(vm, boundary);

        vm.RunSmtcAction(SmtcPlayerAction.PlayPause, isCurrent);
        vm.RunSmtcAction(SmtcPlayerAction.Next, isCurrent);
        vm.RunSmtcAction(SmtcPlayerAction.Previous, isCurrent);

        Assert.Empty(vm.SmtcActions);
    }

    [Fact]
    public void QueuedSmtcCallbackIgnoresUnmappedButton()
    {
        var vm = new PlayerViewModel();

        vm.RunSmtcAction(SmtcPlayerAction.None, vm.CapturePlaybackValidity());

        Assert.Empty(vm.SmtcActions);
    }

    [Fact]
    public async Task MediaSwitchRunsStepsInOrderWhileCurrent()
    {
        var vm = new PlayerViewModel();
        var calls = new List<string>();

        await vm.SwitchMediaAsync(
            () => { calls.Add("pause"); return Task.CompletedTask; },
            () => { calls.Add("stop"); return Task.CompletedTask; },
            () => { calls.Add("initialize"); return Task.CompletedTask; }).WaitAsync(Timeout);

        Assert.Equal(["pause", "stop", "initialize"], calls);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("close")]
    [InlineData("player")]
    [InlineData("client")]
    public async Task MediaSwitchStopsAfterAwaitWhenSessionInvalidated(string boundary)
    {
        var vm = new PlayerViewModel();
        var pauseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        var switching = vm.SwitchMediaAsync(
            () => { calls.Add("pause"); return pauseGate.Task; },
            () => { calls.Add("stop"); return Task.CompletedTask; },
            () => { calls.Add("initialize"); return Task.CompletedTask; });
        Invalidate(vm, boundary);

        pauseGate.SetResult();
        await switching.WaitAsync(Timeout);

        Assert.Equal(["pause"], calls);
    }

    [Fact]
    public async Task MediaSwitchDoesNotInitializeAfterCloseDuringStop()
    {
        var vm = new PlayerViewModel();
        var stopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initializeCalls = 0;
        var switching = vm.SwitchMediaAsync(
            () => Task.CompletedTask,
            () => stopGate.Task,
            () => { initializeCalls++; return Task.CompletedTask; });

        vm.CloseInput();
        stopGate.SetResult();
        await switching.WaitAsync(Timeout);

        Assert.Equal(0, initializeCalls);
    }

    [Fact]
    public async Task MediaSwitchWaitsAcceptedSeekAndAbortsAfterClose()
    {
        var receipt = new TaskCompletionSource<SeekTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new PlayerViewModel();
        vm.Client!.Send = _ => receipt.Task;
        var seek = vm.ChangePositionAsync(21);
        var pauseCalls = 0;
        var switching = vm.SwitchMediaAsync(
            () => { pauseCalls++; return Task.CompletedTask; },
            () => Task.CompletedTask,
            () => Task.CompletedTask);

        vm.CloseInput();
        receipt.SetResult(new(true));
        await Task.WhenAll(seek, switching).WaitAsync(Timeout);

        Assert.Equal(0, pauseCalls);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("close")]
    [InlineData("player")]
    [InlineData("client")]
    public async Task ReplayAfterInvalidationDoesNotWriteOldPlayer(string boundary)
    {
        var vm = new PlayerViewModel();
        var oldPlayer = vm.Player;
        var isCurrent = vm.CapturePlaybackValidity();
        Invalidate(vm, boundary);

        await vm.ReplayCurrentPositionAsync(isCurrent).WaitAsync(Timeout);

        Assert.Empty(oldPlayer.ReplayRequests);
        Assert.Empty(vm.Player.ReplayRequests);
    }

    [Fact]
    public async Task ReplayWhileCurrentWritesCapturedPosition()
    {
        var vm = new PlayerViewModel();
        vm.Player.Position = 21;

        await vm.ReplayCurrentPositionAsync(vm.CapturePlaybackValidity()).WaitAsync(Timeout);

        Assert.Equal([21d], vm.Player.ReplayRequests);
    }

    [Fact]
    public async Task PlaybackInitializationRunsAllStepsWhileCurrent()
    {
        var vm = new PlayerViewModel();
        var calls = new List<string>();

        await PlayerViewModel.RunPlaybackInitializationAsync(vm.CapturePlaybackValidity(), Steps(calls)).WaitAsync(Timeout);

        Assert.Equal(["subtitles", "seek", "stats", "resume", "chapters", "danmaku"], calls);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("close")]
    [InlineData("player")]
    [InlineData("client")]
    public async Task PlaybackInitializationStopsAfterAwaitWhenSessionInvalidated(string boundary)
    {
        var vm = new PlayerViewModel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        var steps = new PlaybackInitializationSteps
        {
            ApplySubtitlesAsync = () => Record(calls, "subtitles"),
            SeekToContinuePositionAsync = () => Record(calls, "seek", gate.Task),
            CloseStatsOverlayAsync = () => Record(calls, "stats"),
            ResumeIfNeededAsync = () => Record(calls, "resume"),
            VerifyChaptersAsync = () => Record(calls, "chapters"),
            InitializeDanmaku = () => calls.Add("danmaku"),
        };

        var running = PlayerViewModel.RunPlaybackInitializationAsync(vm.CapturePlaybackValidity(), steps);
        Invalidate(vm, boundary);
        gate.SetResult();
        await running.WaitAsync(Timeout);

        Assert.Equal(["subtitles", "seek"], calls);
    }

    [Fact]
    public async Task LateDanmakuInitializationSkippedAfterSceneChange()
    {
        var vm = new PlayerViewModel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var danmakuCalls = 0;
        var steps = new PlaybackInitializationSteps
        {
            ApplySubtitlesAsync = () => Task.CompletedTask,
            SeekToContinuePositionAsync = () => Task.CompletedTask,
            CloseStatsOverlayAsync = () => Task.CompletedTask,
            ResumeIfNeededAsync = () => Task.CompletedTask,
            VerifyChaptersAsync = () => gate.Task,
            InitializeDanmaku = () => danmakuCalls++,
        };

        var running = PlayerViewModel.RunPlaybackInitializationAsync(vm.CapturePlaybackValidity(), steps);
        vm.ChangeScene();
        gate.SetResult();
        await running.WaitAsync(Timeout);

        Assert.Equal(0, danmakuCalls);
    }

    private static PlaybackInitializationSteps Steps(List<string> calls) => new()
    {
        ApplySubtitlesAsync = () => Record(calls, "subtitles"),
        SeekToContinuePositionAsync = () => Record(calls, "seek"),
        CloseStatsOverlayAsync = () => Record(calls, "stats"),
        ResumeIfNeededAsync = () => Record(calls, "resume"),
        VerifyChaptersAsync = () => Record(calls, "chapters"),
        InitializeDanmaku = () => calls.Add("danmaku"),
    };

    private static Task Record(List<string> calls, string step, Task? pending = null)
    {
        calls.Add(step);
        return pending ?? Task.CompletedTask;
    }

    private static void Invalidate(PlayerViewModel vm, string boundary)
    {
        switch (boundary)
        {
            case "scene": vm.ChangeScene(); break;
            case "close": vm.CloseInput(); break;
            case "player": vm.Player = new(); break;
            case "client": vm.Client = new(); break;
        }
    }
}
