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
