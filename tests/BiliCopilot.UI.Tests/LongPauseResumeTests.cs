// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Player;
using BiliCopilot.UI.Resolvers;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.MpvKernel.Core.Enums;
using System.Reflection;

namespace BiliCopilot.UI.Tests;

public sealed class LongPauseResumeTests
{
    public LongPauseResumeTests()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
    }

    // ---------- 纯决策：选 loadfile 还是 resume ----------

    [Fact]
    public void SeveredIdleWithPendingPositionChoosesLoadfile()
        => Assert.True(LongPauseResumeDecision.ShouldResumeViaLoadfile(MpvPlayerState.Idle, isSeveredByLongPause: true, 123.4));

    [Theory]
    [InlineData(MpvPlayerState.Paused)]
    [InlineData(MpvPlayerState.Seeking)]
    [InlineData(MpvPlayerState.End)]
    public void NonPlayingResumeIntentChoosesLoadfileWhenSevered(MpvPlayerState state)
        => Assert.True(LongPauseResumeDecision.ShouldResumeViaLoadfile(state, isSeveredByLongPause: true, 50));

    [Fact]
    public void NaturalIdleWithoutSeverChoosesResume()
        => Assert.False(LongPauseResumeDecision.ShouldResumeViaLoadfile(MpvPlayerState.Idle, isSeveredByLongPause: false, 0));

    [Fact]
    public void SeveredWithoutPendingPositionChoosesResume()
        => Assert.False(LongPauseResumeDecision.ShouldResumeViaLoadfile(MpvPlayerState.Idle, isSeveredByLongPause: true, null));

    [Fact]
    public void PlayingNeverChoosesLoadfileEvenIfSevered()
        => Assert.False(LongPauseResumeDecision.ShouldResumeViaLoadfile(MpvPlayerState.Playing, isSeveredByLongPause: true, 10));

    // ---------- 经视图模型：loadfile 回填 + 状态保留 + 位置清理 ----------

    [Fact]
    public async Task ResumeViaLoadfileBackfillsStartPositionAndPreservesSpeedVolume()
    {
        var (vm, _) = await CreateSeveredPlayerAsync();
        vm.IsSeveredByLongPause = true;
        vm.LongPausePendingResumePosition = 4321.5;
        vm.Client!.ReadSpeed = () => 1.5;
        vm.Client.ReadVolume = () => 55;

        var resumed = await vm.ResumeFromLongPauseAsync();

        Assert.True(resumed);
        var load = Assert.Single(vm.Client.Loads);
        Assert.Equal(4321.5, load.Options.StartPosition);
        Assert.Equal(1.5, load.Options.InitialSpeed);
        Assert.Equal(55, load.Options.InitialVolume);
        Assert.False(vm.IsSeveredByLongPause);
        Assert.Null(vm.LongPausePendingResumePosition);
    }

    [Fact]
    public async Task ResumeViaLoadfileRefreshesSourceForExpiredUrl()
    {
        var (vm, service) = await CreateSeveredPlayerAsync();
        vm.IsSeveredByLongPause = true;
        vm.LongPausePendingResumePosition = 100;
        service.Information = new([Segment("80", "https://fresh.example/v")], null!, [new(80, "1080P", false, false)]);

        await vm.ResumeFromLongPauseAsync();

        Assert.Equal("https://fresh.example/v", Assert.Single(vm.Client!.Loads).Url);
        Assert.Equal(2, service.Calls); // initial + refresh re-fetch
    }

    [Fact]
    public async Task FailedLoadfileKeepsPendingResumeForRetry()
    {
        var (vm, _) = await CreateSeveredPlayerAsync();
        vm.IsSeveredByLongPause = true;
        vm.LongPausePendingResumePosition = 77;
        vm.Client!.CompleteLoads = false;

        var resume = vm.ResumeFromLongPauseAsync();
        vm.Client.FailLoad();
        await resume;

        Assert.True(vm.IsSeveredByLongPause);
        Assert.Equal(77, vm.LongPausePendingResumePosition);
        Assert.Single(vm.RefreshErrors);
    }

    // ---------- MoAppHang 回归：loadfile 遗留 paused 不能被当成新长暂停 ----------

    [Fact]
    public async Task ResumedLoadfileResumesPlayback()
    {
        var (vm, _) = await CreateSeveredPlayerAsync();
        vm.IsSeveredByLongPause = true;
        vm.LongPausePendingResumePosition = 100;
        // loadfile 完成后属性观察事件尚未回流，Player 仍是恢复前残留的 Idle。
        vm.Player.PlaybackState = MpvPlayerState.Idle;

        await vm.ResumeFromLongPauseAsync();

        // “继续”意图下必须无条件补 resume，不能按残留状态漏掉。
        Assert.Equal(1, vm.Client!.ResumeCount);
    }

    [Fact]
    public async Task LongPauseSeverSuppressedDuringLoadfileResume()
    {
        var (vm, _) = await CreateSeveredPlayerAsync();
        vm.IsSeveredByLongPause = true;
        vm.LongPausePendingResumePosition = 100;

        await vm.ResumeFromLongPauseAsync();

        // 抑制在恢复开始时开启、结束时解除，全程净变化为 0，且确实被调用过。
        Assert.Equal(0, vm.SeverSuppressCount);
    }

    [Fact]
    public void SuppressedPausedDoesNotArmSeverTimer()
    {
        var fixture = new SeverFixture();
        var monitor = fixture.CreateMonitor();
        monitor.Suppress();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        Assert.Empty(fixture.Timers);
        Assert.False(monitor.IsPending);
    }

    [Fact]
    public void UnsuppressReArmsOnNextPaused()
    {
        var fixture = new SeverFixture();
        var monitor = fixture.CreateMonitor();
        monitor.Suppress();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        monitor.Unsuppress();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        Assert.Single(fixture.Timers);
        Assert.True(monitor.IsPending);
    }

    [Fact]
    public void ResetClearsSuppression()
    {
        var fixture = new SeverFixture();
        var monitor = fixture.CreateMonitor();
        monitor.Suppress();
        monitor.Reset();
        monitor.OnPlaybackStateChanged(MpvPlayerState.Paused, isLive: false);
        Assert.Single(fixture.Timers);
    }

    private sealed class SeverFixture
    {
        public List<FakeSeverTimer> Timers { get; } = [];
        public LongPauseSeverMonitor CreateMonitor()
            => new(LongPauseSeverPolicy.Threshold, delay => { var t = new FakeSeverTimer(); Timers.Add(t); return t; }, _ => { });
    }

    private sealed class FakeSeverTimer : ILongPauseSeverTimer
    {
        public void Cancel() { }
    }

    private static DashSegmentInformation Segment(string id, string url)
        => new(id, url, 100, "", "avc", 0, 0, "", "", []);

    private static async Task<(PlayerViewModel Vm, PlaybackRoutingResolverTests.PlaybackService Service)> CreateSeveredPlayerAsync()
    {
        var api = DispatchProxy.Create<IPlayerService, PlaybackRoutingResolverTests.PlaybackService>();
        var service = (PlaybackRoutingResolverTests.PlaybackService)(object)api;
        service.Information = new([Segment("80", "https://video.example/v")], null!, [new(80, "1080P", false, false)]);
        var resolver = PlaybackRoutingResolverTests.CreateResolver(false, api);
        await resolver.GetSourceAsync();
        var vm = new PlayerViewModel();
        vm.SetRefreshResolver(resolver);
        return (vm, service);
    }
}
