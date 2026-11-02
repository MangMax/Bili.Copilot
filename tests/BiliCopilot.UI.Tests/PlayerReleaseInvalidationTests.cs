// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Player;
using BiliCopilot.UI.ViewModels.Core;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Danmaku;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 释放/回滚必须先让视图模型失效：迟到回调不得再写已释放的客户端。
/// </summary>
public class PlayerReleaseInvalidationTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public PlayerReleaseInvalidationTests() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = new();

    public void Dispose() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = null;

    [Fact]
    public void InvalidateForRelease_InvalidatesCapturedAndFutureValidity()
    {
        var vm = new PlayerViewModel();
        var captured = vm.CapturePlaybackValidity();
        Assert.True(captured());
        Assert.True(vm.IsSessionCurrent());

        vm.InvalidateForRelease();

        Assert.False(captured());
        Assert.False(vm.CapturePlaybackValidity()());
        Assert.False(vm.IsSessionCurrent());
    }

    [Fact]
    public void InvalidateForRelease_IsIdempotent()
    {
        var vm = new PlayerViewModel();

        vm.InvalidateForRelease();
        vm.InvalidateForRelease();

        Assert.False(vm.IsSessionCurrent());
    }

    [Fact]
    public void QueuedSmtcActionCapturedBeforeInvalidationRunsNothing()
    {
        var vm = new PlayerViewModel();
        var captured = vm.CapturePlaybackValidity();

        vm.InvalidateForRelease();
        vm.RunSmtcAction(SmtcPlayerAction.PlayPause, captured);

        Assert.Empty(vm.SmtcActions);
    }

    [Fact]
    public async Task ReplayCapturedBeforeInvalidationWritesNothing()
    {
        var vm = new PlayerViewModel();
        var oldPlayer = vm.Player;
        var captured = vm.CapturePlaybackValidity();

        vm.InvalidateForRelease();
        await vm.ReplayCurrentPositionAsync(captured).WaitAsync(Timeout);

        Assert.Empty(oldPlayer.ReplayRequests);
    }

    [Fact]
    public async Task ReleasePathInvalidatesBeforeReleasingResources()
    {
        var vm = new PlayerViewModel();
        var captured = vm.CapturePlaybackValidity();
        var wasCurrentDuringRelease = true;

        await vm.ReleaseSessionCoreAsync(() =>
        {
            wasCurrentDuringRelease = captured();
            return Task.CompletedTask;
        }).WaitAsync(Timeout);

        Assert.False(wasCurrentDuringRelease);
        Assert.False(vm.CapturePlaybackValidity()());
    }

    [Fact]
    public async Task RollingBackFailedInitializationReachesInvalidation()
    {
        var vm = new PlayerViewModel();
        var session = new PlaybackSession<PlayerViewModel>(
            _ => vm,
            value => value.ReleaseSessionCoreAsync(() => Task.CompletedTask),
            _ => { });
        var captured = vm.CapturePlaybackValidity();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.InitializeAsync(() => throw new InvalidOperationException("mpv"))).WaitAsync(Timeout);

        Assert.True(session.IsReleased);
        Assert.False(captured());
    }

    [Fact]
    public async Task InvalidateForRelease_DisposesDanmakuLoadSession()
    {
        var vm = new PlayerViewModel();
        var service = new RecordingDanmakuService();
        var loader = new BiliCopilot.UI.Danmaku.DanmakuLoadSession(service, action => action(), _ => { });
        vm.DanmakuLoadSession = loader;

        vm.InvalidateForRelease();
        await loader.LoadAsync("1", "2", 1).WaitAsync(Timeout);

        Assert.Equal(0, service.Requests);
    }

    private sealed class RecordingDanmakuService : IDanmakuService
    {
        public int Requests { get; private set; }

        public Task<IReadOnlyList<DanmakuInformation>> GetSegmentDanmakusWithGrpcAsync(string aid, string cid, int segmentIndex, CancellationToken cancellationToken = default)
        {
            Requests++;
            return Task.FromResult<IReadOnlyList<DanmakuInformation>>([]);
        }

        public Task<DanmakuMeta> GetDanmakuMetaAsync(string aid, string cid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<DanmakuInformation>> GetSegmentDanmakusWithWebAsync(string aid, string cid, int segmentIndex, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SendVideoDanmakuAsync(string content, string aid, string cid, int progress, string color, bool isStandardSize = true, DanmakuLocation location = DanmakuLocation.Scroll, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SendLiveDanmakuAsync(string content, string roomId, string color, bool isStandardSize = true, DanmakuLocation location = DanmakuLocation.Scroll, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
