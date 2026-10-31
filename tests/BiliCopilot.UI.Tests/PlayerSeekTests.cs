// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;

namespace BiliCopilot.UI.Tests;

public class PlayerSeekTests : IDisposable
{
    public PlayerSeekTests() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = new();

    public void Dispose() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = null;

    [Theory]
    [InlineData("scene")]
    [InlineData("close")]
    [InlineData("player")]
    [InlineData("client")]
    public void CapturedPlaybackValidityRejectsReplacedSession(string change)
    {
        var vm = new PlayerViewModel();
        var isCurrent = vm.CapturePlaybackValidity();
        Assert.True(isCurrent());
        switch (change)
        {
            case "scene": vm.ChangeScene(); break;
            case "close": vm.CloseInput(); break;
            case "player": vm.Player = new(); break;
            case "client": vm.Client = new(); break;
        }

        Assert.False(isCurrent());
    }

    [Fact]
    public async Task CapturedPlaybackValiditySurvivesSameMediaSeek()
    {
        var vm = new PlayerViewModel();
        var isCurrent = vm.CapturePlaybackValidity();
        await vm.ChangePositionAsync(20);
        Assert.True(isCurrent());
        vm.ChangeScene();
        Assert.False(isCurrent());
        Assert.True(vm.CapturePlaybackValidity()());
    }

    [Theory]
    [InlineData(22)]
    [InlineData(20)]
    public async Task ShortSeekReachesMediaClient(double target)
    {
        var vm = new PlayerViewModel();
        await vm.ChangePositionAsync(target);
        Assert.Equal([target], vm.Client!.Requests);
        Assert.Equal((int)target, vm.Danmaku.Position);
    }

    [Fact]
    public async Task SameTargetExplicitSeekRequestsReconstruction()
    {
        var vm = new PlayerViewModel();
        var seeks = new List<int>();
        vm.Danmaku.SeekRequested += (_, p) => seeks.Add(p);
        await vm.ChangePositionAsync(21);
        await vm.ChangePositionAsync(21);
        Assert.Equal([21, 21], seeks);
    }

    [Fact]
    public void NormalProgressDoesNotRequestReconstruction()
    {
        var vm = new DanmakuRenderViewModel();
        var seeks = new List<int>();
        var progress = new List<int>();
        vm.SeekRequested += (_, p) => seeks.Add(p);
        vm.ProgressChanged += (_, p) => progress.Add(p);
        vm.Resume();
        vm.UpdatePosition(21);
        vm.UpdatePosition(22);
        Assert.Empty(seeks);
        Assert.Equal([21, 22], progress);
    }

    [Fact]
    public void PausedPositionChangeReconstructsButDuplicateTickDoesNot()
    {
        var vm = new DanmakuRenderViewModel();
        var seeks = new List<int>();
        vm.SeekRequested += (_, p) => seeks.Add(p);
        vm.UpdatePosition(21);
        vm.Pause();
        vm.UpdatePosition(22);
        vm.UpdatePosition(22);
        vm.UpdatePosition(20);
        Assert.Equal([22, 20], seeks);
    }

    [Fact]
    public async Task PlayingUserSeekStillRequestsReconstruction()
    {
        var vm = new PlayerViewModel();
        var seeks = new List<int>();
        vm.Danmaku.Resume();
        vm.Danmaku.SeekRequested += (_, p) => seeks.Add(p);
        await vm.ChangePositionAsync(22);
        Assert.Equal([22], seeks);
    }

    [Fact]
    public void MediaPropertiesCarryPausePositionAndSpeed()
    {
        var vm = new PlayerViewModel();
        vm.Player.PlaybackState = Richasy.MpvKernel.Core.Enums.MpvPlayerState.Paused;
        vm.Player.Notify(nameof(vm.Player.PlaybackState));
        vm.Player.Position = 22;
        vm.Player.Notify(nameof(vm.Player.Position));
        vm.Player.PlaybackRate = 1.5;
        vm.Player.Notify(nameof(vm.Player.PlaybackRate));
        Assert.True(vm.Danmaku.IsPaused);
        Assert.Equal(22, vm.Danmaku.Position);
        Assert.Equal(1.5, vm.Danmaku.ExtraSpeed);
        vm.Player.PlaybackState = Richasy.MpvKernel.Core.Enums.MpvPlayerState.Playing;
        vm.Player.Notify(nameof(vm.Player.PlaybackState));
        Assert.False(vm.Danmaku.IsPaused);
    }

    [Fact]
    public async Task EndSeekReplaysAndReconstructsTarget()
    {
        var vm = new PlayerViewModel();
        vm.Player.PlaybackState = Richasy.MpvKernel.Core.Enums.MpvPlayerState.End;
        await vm.ChangePositionAsync(20);
        Assert.Equal([20d], vm.Player.ReplayRequests);
        Assert.Empty(vm.Client!.Requests);
        Assert.Equal(20, vm.Danmaku.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OldOrClosedMediaCallbacksCannotAdvanceTarget(bool close)
    {
        var vm = new PlayerViewModel();
        var previous = vm.Player;
        if (close) vm.CloseInput();
        else vm.Player = new();
        previous.Position = 22;
        previous.Notify(nameof(previous.Position));
        Assert.Equal(0, vm.Danmaku.Position);
    }

    [Fact]
    public async Task LateSeekReceiptCannotReplaceLatestTarget()
    {
        var old = new TaskCompletionSource<SeekTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new PlayerViewModel();
        vm.Client!.Send = async p =>
        {
            if (p == 22) await old.Task;
            vm.Player.Position = p;
            vm.Player.Notify(nameof(vm.Player.Position));
            return new(true);
        };
        var pending = vm.ChangePositionAsync(22);
        var latest = vm.ChangePositionAsync(25);
        old.SetResult(new(true));
        await Task.WhenAll(pending, latest).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(25, vm.Player.Position);
        Assert.Equal(25, vm.Danmaku.Position);
    }

    [Fact]
    public async Task ConcurrentSeeksCommitLatestMediaTarget()
    {
        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new PlayerViewModel();
        vm.Player.Position = 20;
        vm.Client!.Send = async p =>
        {
            // Model the thread-pool native setter: its write happens before its receipt,
            // but independently started calls can reach the media in a different order.
            if (p == 21) await firstWrite.Task;
            vm.Player.Position = p;
            vm.Player.Notify(nameof(vm.Player.Position));
            return new(true);
        };
        var requests = Task.WhenAll(vm.ChangePositionAsync(21), vm.ChangePositionAsync(19), vm.ChangePositionAsync(20));
        firstWrite.SetResult();
        await requests.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(20, vm.Player.Position);
        Assert.Equal(20, vm.Danmaku.Position);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("close")]
    [InlineData("player")]
    [InlineData("client")]
    public async Task InvalidatedQueuedSeekNeverWritesMedia(string boundary)
    {
        var receipt = new TaskCompletionSource<SeekTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new PlayerViewModel();
        var client = vm.Client!;
        client.Send = _ => receipt.Task;
        var active = vm.ChangePositionAsync(21);
        var queued = vm.ChangePositionAsync(20);
        switch (boundary)
        {
            case "scene": vm.ChangeScene(); break;
            case "close": vm.CloseInput(); break;
            case "player": vm.Player = new(); break;
            case "client": vm.Client = new(); break;
        }

        receipt.SetResult(new(true));
        await Task.WhenAll(active, queued).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([21d], client.Requests);
        if (boundary == "client") Assert.Empty(vm.Client!.Requests);
        Assert.Equal(0, vm.Danmaku.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedWriteReleasesLatestQueuedSeek(bool throws)
    {
        var receipt = new TaskCompletionSource<SeekTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new PlayerViewModel();
        vm.Client!.Send = p => p == 21 ? receipt.Task : Task.FromResult(new SeekTestResult(true));
        var active = vm.ChangePositionAsync(21);
        var stale = vm.ChangePositionAsync(19);
        var latest = vm.ChangePositionAsync(20);
        if (throws) receipt.SetException(new InvalidOperationException("native write failed"));
        else receipt.SetResult(new(false));
        if (throws) await Assert.ThrowsAsync<InvalidOperationException>(() => active.WaitAsync(TimeSpan.FromSeconds(5)));
        else await active.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(stale, latest).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([21d, 20d], vm.Client.Requests);
        Assert.Equal(20, vm.Danmaku.Position);
    }

    [Fact]
    public async Task CloseDrainsActiveWriteAndRejectsQueuedTarget()
    {
        var receipt = new TaskCompletionSource<SeekTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new PlayerViewModel();
        vm.Client!.Send = _ => receipt.Task;
        var active = vm.ChangePositionAsync(21);
        var queued = vm.ChangePositionAsync(20);
        vm.CloseInput();
        var drain = vm.DrainPositionChangesAsync();
        Assert.False(drain.IsCompleted);
        receipt.SetResult(new(true));
        await Task.WhenAll(active, queued, drain).WaitAsync(TimeSpan.FromSeconds(5));
        await vm.ChangePositionAsync(25);
        Assert.Equal([21d], vm.Client.Requests);
        Assert.Equal(0, vm.Danmaku.Position);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task InvalidTargetDoesNotReachMediaClient(double target)
    {
        var vm = new PlayerViewModel();
        await vm.ChangePositionAsync(target);
        Assert.Empty(vm.Client!.Requests);
        Assert.Equal(0, vm.Danmaku.Position);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("close")]
    [InlineData("player")]
    [InlineData("client")]
    [InlineData("failed")]
    public async Task InvalidatedReceiptCannotPublish(string boundary)
    {
        var receipt = new TaskCompletionSource<SeekTestResult>();
        var vm = new PlayerViewModel();
        var seeks = new List<int>();
        vm.Danmaku.SeekRequested += (_, p) => seeks.Add(p);
        vm.Client!.Send = _ => receipt.Task;
        var pending = vm.ChangePositionAsync(22);
        switch (boundary)
        {
            case "scene": vm.ChangeScene(); break;
            case "close": vm.CloseInput(); break;
            case "player": vm.Player = new(); break;
            case "client": vm.Client = new(); break;
        }

        receipt.SetResult(new(boundary != "failed"));
        await pending;
        Assert.Empty(seeks);
        Assert.Equal(0, vm.Danmaku.Position);
    }
}

// Only playback IO is adapted. The public seek command is linked production source.
