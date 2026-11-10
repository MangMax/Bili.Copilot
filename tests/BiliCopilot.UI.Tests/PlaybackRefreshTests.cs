using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Resolvers;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging.Abstractions;
using BiliCopilot.UI.PlaybackRouting;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Components;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using System.Reflection;

namespace BiliCopilot.UI.Tests;

public sealed class PlaybackRefreshTests
{
    public PlaybackRefreshTests()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
    }

    [Fact]
    public async Task PlayerRefresh_PgcPreviewWithoutDashDoesNotOfferRefresh()
    {
        var api = DispatchProxy.Create<IPlayerService, PlaybackRoutingResolverTests.PlaybackService>();
        var service = (PlaybackRoutingResolverTests.PlaybackService)(object)api;
        service.Information = new(null, null, null, isPreview: true, previewUrls: ["https://preview.example/video"]);
        var resolver = PlaybackRoutingResolverTests.CreateResolver(true, api);
        Assert.Equal("https://preview.example/video", (await resolver.GetSourceAsync()).Url);
        var vm = new PlayerViewModel();
        vm.SetRefreshResolver(resolver);

        Assert.False(vm.IsPlaybackRefreshAvailable);
        Assert.False(vm.RefreshPlaybackCommand.CanExecute(null));
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Empty(vm.Client!.Loads);
        Assert.Empty(vm.RefreshErrors);
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task PlayerRefresh_PgcDashOffersAndExecutesRefresh()
    {
        var api = DispatchProxy.Create<IPlayerService, PlaybackRoutingResolverTests.PlaybackService>();
        var service = (PlaybackRoutingResolverTests.PlaybackService)(object)api;
        service.Information = new([new("80", "https://video.example/v", 100, "", "avc", 0, 0, "", "", [])], null, [new(80, "1080P", false, false)]);
        var resolver = PlaybackRoutingResolverTests.CreateResolver(true, api);
        await resolver.GetSourceAsync();
        var vm = new PlayerViewModel();
        vm.SetRefreshResolver(resolver);

        Assert.True(vm.IsPlaybackRefreshAvailable);
        Assert.True(vm.RefreshPlaybackCommand.CanExecute(null));
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Equal("https://video.example/v", Assert.Single(vm.Client!.Loads).Url);
        Assert.Equal(2, service.Calls);
    }

    [Fact]
    public async Task PlayerRefresh_UsesFreshSourceAndReloadsWithCurrentPositionSpeedVolume()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var api = DispatchProxy.Create<IPlayerService, PlaybackRoutingResolverTests.PlaybackService>();
        var service = (PlaybackRoutingResolverTests.PlaybackService)(object)api;
        service.Information = new([new("80", "https://video.example/v", 100, "", "avc", 0, 0, "", "", [])], null!, [new(80, "1080P", false, false)]);
        var source = PlaybackRoutingResolverTests.CreateResolver(false, api);
        await source.GetSourceAsync();
        var vm = new PlayerViewModel();
        vm.SetRefreshResolver(source);
        vm.Player.Position = 24;
        vm.Player.PlaybackRate = 1.75;
        vm.Player.Volume = 37;
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Equal(2, service.Calls);
        var load = Assert.Single(vm.Client!.Loads);
        Assert.Equal("https://video.example/v", load.Url);
        Assert.Equal(24, load.Options.StartPosition);
        Assert.Equal(1.75, load.Options.InitialSpeed);
        Assert.Equal(37, load.Options.InitialVolume);
    }

    [Fact]
    public async Task PlayerRefresh_NewerSeekDuringFetchCancelsReload()
    {
        var (vm, api) = await CreatePlayerAsync();
        var delayed = new TaskCompletionSource<Richasy.BiliKernel.Models.Media.DashMediaInformation>();
        api.Fetch = () => delayed.Task;
        var refresh = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        await vm.ChangePositionAsync(42);
        delayed.SetResult(api.Information);
        await refresh;
        Assert.Empty(vm.Client!.Loads);
        Assert.Equal(42, Assert.Single(vm.Client.Requests));
    }

    [Fact]
    public async Task PlayerRefresh_NewerSeekAndSpeedWaitForNativeLoad()
    {
        var (vm, _) = await CreatePlayerAsync();
        vm.Client!.CompleteLoads = false;
        var refresh = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Single(vm.Client.Loads);
        var seek = vm.ChangePositionAsync(42);
        var speed = vm.ChangeSpeedAsync(2);
        Assert.Empty(vm.Client.Requests);
        Assert.Empty(vm.Client.Speeds);
        vm.Client.CompleteLoad();
        await Task.WhenAll(refresh, seek, speed);
        Assert.Equal(42, Assert.Single(vm.Client.Requests));
        Assert.Equal(2, Assert.Single(vm.Client.Speeds));
    }

    [Fact]
    public async Task PlayerRefresh_CloseWaitsForAdmittedLoadAndRejectsLateFetch()
    {
        var (vm, _) = await CreatePlayerAsync();
        vm.Client!.CompleteLoads = false;
        var refresh = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        var released = false;
        var close = vm.ReleaseSessionCoreAsync(() => { released = true; return Task.CompletedTask; });
        Assert.False(released);
        vm.Client.CompleteLoad();
        await Task.WhenAll(close, refresh);
        Assert.True(released);
        Assert.False(vm.RefreshPlaybackCommand.CanExecute(null));
        Assert.Empty(vm.RefreshErrors);
    }

    [Fact]
    public async Task PlayerRefresh_LoadFailureIsReportedAndButtonCanRetry()
    {
        var (vm, _) = await CreatePlayerAsync();
        vm.Client!.CompleteLoads = false;
        var refresh = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.False(vm.RefreshPlaybackCommand.CanExecute(null));
        vm.Client.FailLoad();
        await refresh;
        Assert.Single(vm.RefreshErrors);
        Assert.True(vm.RefreshPlaybackCommand.CanExecute(null));
    }


    [Fact]
    public async Task PlayerRefresh_LiveOffersRefreshAndReopensWithoutPositionOrSpeed()
    {
        var (resolver, service) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        Assert.True(vm.IsPlaybackRefreshAvailable);
        Assert.True(vm.RefreshPlaybackCommand.CanExecute(null));

        vm.Player.Position = 24;
        vm.Player.PlaybackRate = 1.75;
        vm.Player.Volume = 37;
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);

        var load = Assert.Single(vm.Client!.Loads);
        Assert.Equal(2, service.PlayCalls);
        Assert.Null(load.Options.StartPosition);
        Assert.Equal(1d, load.Options.InitialSpeed);
        Assert.Equal(37d, load.Options.InitialVolume);
        Assert.Empty(vm.RefreshErrors);
    }

    [Fact]
    public async Task PlayerRefresh_LiveRefreshIsSingleFlightAndLateFetchIsDiscardedAfterClose()
    {
        var (resolver, service) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        var pending = new TaskCompletionSource<LiveMediaInformation>();
        service.Fetch = () => pending.Task;

        var refresh = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.False(vm.RefreshPlaybackCommand.CanExecute(null));
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        await vm.ReleaseSessionCoreAsync(() => Task.CompletedTask);
        pending.SetResult(service.Information);
        await refresh;

        Assert.Empty(vm.Client!.Loads);
        Assert.Empty(vm.RefreshErrors);
        Assert.False(resolver.IsRefreshPending);
    }

    [Fact]
    public async Task PlayerRefresh_LiveFetchFailureIsReportedAndButtonCanRetry()
    {
        var (resolver, service) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        service.Fetch = () => Task.FromException<LiveMediaInformation>(
            new HttpRequestException("403", null, System.Net.HttpStatusCode.Forbidden));

        await vm.RefreshPlaybackCommand.ExecuteAsync(null);

        Assert.Single(vm.RefreshErrors);
        Assert.Empty(vm.Client!.Loads);
        Assert.True(vm.RefreshPlaybackCommand.CanExecute(null));
    }

    [Fact]
    public async Task LiveRefreshFailedNativeLoadRetriesWithinSameOperation()
    {
        var (resolver, service) = await CreateLiveResolverAsync();
        service.Information = new LiveMediaInformation("123", [],
            [new LiveLineInformation("avc", 400, [400],
                [new LivePlayUrl("http_hls", "https://first.example", "/a.m3u8", ""),
                 new LivePlayUrl("http_hls", "https://backup.example", "/a.m3u8", "")])]);
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        vm.Client!.OnLoad = () =>
        {
            if (vm.Client.Loads.Count == 1) vm.Client.FailLoad();
            else vm.Client.CompleteLoad();
        };
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Client.Loads.Count);
        Assert.Equal("backup.example", new Uri(vm.Client.Loads[1].Url).Host);
        Assert.Empty(vm.RefreshErrors);
        Assert.True(vm.RefreshPlaybackCommand.CanExecute(null));
    }

    [Fact]
    public async Task LiveRefreshExhaustsCandidatesThenRequestsFreshUrlWithinSameOperation()
    {
        var (resolver, service) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        vm.Client!.OnLoad = () =>
        {
            if (vm.Client.Loads.Count == 1) vm.Client.FailLoad();
            else vm.Client.CompleteLoad();
        };
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Client.Loads.Count);
        Assert.Equal(3, service.PlayCalls); // Initial, manual refresh, then recovery refresh.
        Assert.Empty(vm.RefreshErrors);
    }

    [Fact]
    public async Task LiveRefreshStopsAfterThreeFailedNativeLoadsWithOriginalFailureKind()
    {
        var (resolver, service) = await CreateLiveResolverAsync();
        service.Information = new LiveMediaInformation("123", [],
            [new LiveLineInformation("avc", 400, [400],
                Enumerable.Range(0, 5).Select(i => new LivePlayUrl("http_hls", $"https://host{i}.example", "/a.m3u8", "")).ToArray())]);
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        vm.Client!.OnLoad = vm.Client.FailLoad;
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.Client.Loads.Count);
        var error = Assert.IsType<LivePlaybackUnavailableException>(Assert.Single(vm.RefreshErrors));
        Assert.Equal(LivePlaybackFailureKind.Transport, error.Kind);
        Assert.True(vm.RefreshPlaybackCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(Richasy.MpvKernel.MpvError.Http403, LivePlaybackFailureKind.Transport)]
    [InlineData(Richasy.MpvKernel.MpvError.TlsError, LivePlaybackFailureKind.Transport)]
    [InlineData(Richasy.MpvKernel.MpvError.VoInitFailed, LivePlaybackFailureKind.DecoderInit)]
    public void NativeFailureClassificationPreservesTransportAndDecoderErrors(Richasy.MpvKernel.MpvError error, LivePlaybackFailureKind expected)
        => Assert.Equal(expected, LiveNativePlaybackFailure.Classify(error));

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task LiveReloadResumesTerminalOwnedPauseButPreservesUserPause(bool userPaused, int expectedResumes)
    {
        var (resolver, _) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        vm.Player.PlaybackState = userPaused
            ? Richasy.MpvKernel.Core.Enums.MpvPlayerState.Paused
            : Richasy.MpvKernel.Core.Enums.MpvPlayerState.Playing;
        vm.RememberLiveTerminalPause();
        vm.Player.PlaybackState = Richasy.MpvKernel.Core.Enums.MpvPlayerState.Paused;
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Equal(expectedResumes, vm.Client!.ResumeCount);
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Equal(expectedResumes, vm.Client.ResumeCount);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task OnlyExplicitPauseIntentRevokesTerminalResumeOwnership(bool changesPause, int expectedResumes)
    {
        var (resolver, _) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        vm.Player.PlaybackState = Richasy.MpvKernel.Core.Enums.MpvPlayerState.Playing;
        vm.RememberLiveTerminalPause();
        Assert.True(await vm.PreparePlaybackIntentAsync(changesPause));
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Equal(expectedResumes, vm.Client!.ResumeCount);
    }

    [Fact]
    public async Task TerminalPauseArrivingBehindQueuedPositionCannotDeadlockAdmittedReload()
    {
        var (resolver, _) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        var position = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.SetPositionQueueTask(position.Task);
        var refresh = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Empty(vm.Client!.Loads);
        // A later terminal pause waits for the already-admitted native reload.
        vm.SetTerminalPauseTask(vm.DrainPositionChangesAsync());
        position.SetResult();
        await refresh.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(vm.Client.Loads);
        Assert.Empty(vm.RefreshErrors);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task LiveAfterTerminalDuringNativeReloadWaitsAndReloadsFreshInsteadOfLosingStart(bool userPaused, int expectedResumes)
    {
        var (resolver, service) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        vm.Player.PlaybackState = userPaused
            ? Richasy.MpvKernel.Core.Enums.MpvPlayerState.Paused
            : Richasy.MpvKernel.Core.Enums.MpvPlayerState.Playing;
        vm.Client!.CompleteLoads = false;
        var admitted = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        Assert.Single(vm.Client.Loads);
        vm.ApplyLiveRoomTerminal(LiveRoomPhase.NotStarted, null);
        vm.ClearLiveRoomTerminal();
        var started = vm.RequestLivePlayUrlReloadAsync();
        Assert.False(started.IsCompleted);
        Assert.Single(vm.Client.Loads);
        vm.Client.CompleteLoads = true;
        vm.Client.CompleteLoad();
        await Task.WhenAll(admitted, started).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, vm.Client.Loads.Count);
        Assert.Equal(3, service.PlayCalls);
        Assert.Equal(0, vm.Client.PauseCount);
        Assert.Equal(expectedResumes, vm.Client.ResumeCount);
        Assert.False(vm.IsLiveRoomTerminal);
        Assert.Empty(vm.RefreshErrors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingLiveStartIsDiscardedAfterCloseOrNewTerminalEvent(bool close)
    {
        var (resolver, service) = await CreateLiveResolverAsync();
        var vm = new PlayerViewModel { IsLive = true };
        vm.SetRefreshResolver(resolver);
        vm.Player.PlaybackState = Richasy.MpvKernel.Core.Enums.MpvPlayerState.Playing;
        vm.Client!.CompleteLoads = false;
        var admitted = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        vm.ApplyLiveRoomTerminal(LiveRoomPhase.NotStarted, null);
        vm.ClearLiveRoomTerminal();
        var started = vm.RequestLivePlayUrlReloadAsync();
        if (close) vm.CloseInput();
        else vm.ApplyLiveRoomTerminal(LiveRoomPhase.CutOff, null);
        vm.Client.CompleteLoad();
        await Task.WhenAll(admitted, started).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(vm.Client.Loads);
        Assert.Equal(2, service.PlayCalls);
        Assert.Equal(0, vm.Client.ResumeCount);
        Assert.Empty(vm.RefreshErrors);
    }

    private static async Task<(LiveMediaSourceResolver Resolver, LiveMediaSourceResolverTests.LivePlayerService Service)> CreateLiveResolverAsync()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var api = DispatchProxy.Create<IPlayerService, LiveMediaSourceResolverTests.LivePlayerService>();
        var service = (LiveMediaSourceResolverTests.LivePlayerService)(object)api;
        var page = new LivePlayerView(
            new LiveInformation(new MediaIdentifier("123", "房间标题", null)),
            new LiveTag("area", "分区", "default"));
        service.View = page;
        service.Information = new LiveMediaInformation(
            "123",
            [new PlayerFormatInformation(400, "蓝光", false, false)],
            [new LiveLineInformation("avc", 400, [400], [new LivePlayUrl("http_hls", "https://hls.example", "/live/a.m3u8", "?sign=1")])]);
        var cookies = DispatchProxy.Create<IBiliCookiesResolver, LiveMediaSourceResolverTests.CookieService>();
        var resolver = new LiveMediaSourceResolver(api, cookies, NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "房间标题", null))));
        await resolver.GetSourceAsync();
        return (resolver, service);
    }

    private static async Task<(PlayerViewModel Player, PlaybackRoutingResolverTests.PlaybackService Api)> CreatePlayerAsync()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var api = DispatchProxy.Create<IPlayerService, PlaybackRoutingResolverTests.PlaybackService>();
        var service = (PlaybackRoutingResolverTests.PlaybackService)(object)api;
        service.Information = new([new("80", "https://video.example/v", 100, "", "avc", 0, 0, "", "", [])], null!, [new(80, "1080P", false, false)]);
        var source = PlaybackRoutingResolverTests.CreateResolver(false, api);
        await source.GetSourceAsync();
        var vm = new PlayerViewModel();
        vm.SetRefreshResolver(source);
        return (vm, service);
    }

    [Fact]
    public async Task PlayerRefresh_CapturesNativeStateAfterQueuedChangesInsteadOfStaleUiPolling()
    {
        var (vm, _) = await CreatePlayerAsync();
        vm.Client!.ReadPosition = () => 34;
        vm.Client.ReadSpeed = () => 2.25;
        vm.Client.ReadVolume = () => 43;
        await vm.RefreshPlaybackCommand.ExecuteAsync(null);
        var load = Assert.Single(vm.Client.Loads);
        Assert.Equal(34, load.Options.StartPosition);
        Assert.Equal(2.25, load.Options.InitialSpeed);
        Assert.Equal(43, load.Options.InitialVolume);
    }

    [Theory]
    [InlineData(0, "https://safe.example/v", "https://other.example/v", "other.example")]
    [InlineData(0, "https://safe.example/v", "https://x.mountaintoys.cn/v", "safe.example")]
    [InlineData(1, "https://safe.example/v", "https://other.example/v", "safe.example")]
    [InlineData(2, "https://primary.example/v", "https://safe.example/v", "safe.example")]
    [InlineData(3, "https://upos-sz-mirrorali.bilivideo.com/v", "https://other.example/v", "upos-sz-mirrorali.bilivideo.com")]
    [InlineData(3, "https://upos-sz-mirrorali.bilivideo.com/v", "https://upos-sz-mirroralib.bilivideo.com/v", "upos-sz-mirroralib.bilivideo.com")]
    [InlineData(5, "https://safe.example/v", "https://other.example/v", "other.example")]
    [InlineData(0, "https://safe.example/v", "https://safe.example/backup", "safe.example")]
    public void Refresh_HostAvoidanceIsOnlyATieBreakInsidePreferenceAndRisk(int preference, string primary, string backup, string expected)
    {
        var previous = preference == 2 ? new Uri(backup).Host : new Uri(primary).Host;
        var selected = PlaybackRouteSelector.Select(primary, [backup], (PlaybackRoutePreference)preference, false, previous);
        Assert.Equal(expected, new Uri(selected!).Host);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlayerRefresh_NewIntentGateOrMediaSwitchDuringFetchPreventsReload(bool switching)
    {
        var (vm, api) = await CreatePlayerAsync();
        var delayed = new TaskCompletionSource<Richasy.BiliKernel.Models.Media.DashMediaInformation>();
        api.Fetch = () => delayed.Task;
        var refresh = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        if (switching)
            await vm.SwitchMediaAsync(() => Task.CompletedTask, () => Task.CompletedTask, () => { vm.ChangeScene(); return Task.CompletedTask; });
        else
            Assert.True(await vm.PreparePlaybackIntentAsync()); // Same gate used by pause, mute and volume commands.
        delayed.SetResult(api.Information);
        await refresh;
        Assert.Empty(vm.Client!.Loads);
        Assert.Empty(vm.RefreshErrors);
    }

    [Fact]
    public async Task PlayerRefresh_CloseDuringFetchDiscardsLateSource()
    {
        var (vm, api) = await CreatePlayerAsync();
        var delayed = new TaskCompletionSource<Richasy.BiliKernel.Models.Media.DashMediaInformation>();
        api.Fetch = () => delayed.Task;
        var refresh = vm.RefreshPlaybackCommand.ExecuteAsync(null);
        await vm.ReleaseSessionCoreAsync(() => Task.CompletedTask);
        delayed.SetResult(api.Information);
        await refresh;
        Assert.Empty(vm.Client!.Loads);
        Assert.Empty(vm.RefreshErrors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_ReportsFetchFailureOrTimeoutAndAllowsRetry(bool timeout)
    {
        var controller = new PlaybackRefreshController();
        var errors = new List<Exception>();
        var loads = 0;
        await controller.RefreshAsync(_ => timeout ? new TaskCompletionSource<string>().Task
            : Task.FromException<string>(new InvalidOperationException("network")),
            (_, _) => { loads++; return Task.CompletedTask; }, () => true, errors.Add, TimeSpan.FromMilliseconds(20));
        Assert.Single(errors);
        Assert.Equal(0, loads);
        Assert.False(controller.IsRefreshing);
        await controller.RefreshAsync(_ => Task.FromResult("new"), (_, _) => { loads++; return Task.CompletedTask; }, () => true, errors.Add);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Refresh_StaleSessionDoesNotReloadOrShowAnError()
    {
        var controller = new PlaybackRefreshController();
        var pending = new TaskCompletionSource<string>();
        var current = true;
        var task = controller.RefreshAsync(_ => pending.Task, (_, _) => { Assert.Fail("Stale reload"); return Task.CompletedTask; },
            () => current, _ => Assert.Fail("Stale error"));
        current = false;
        pending.SetResult("old");
        await task;
        Assert.False(controller.IsRefreshing);
    }

    [Fact]
    public async Task Refresh_IsSingleFlightAndCancelledFetchNeverReopens()
    {
        var controller = new PlaybackRefreshController();
        var pending = new TaskCompletionSource<string>();
        var requests = 0;
        var reloads = 0;
        Task<string> Fetch(CancellationToken token) { requests++; return pending.Task; }
        Task Reload(string source, CancellationToken token) { reloads++; return Task.CompletedTask; }
        var first = controller.RefreshAsync(Fetch, Reload, () => true, _ => Assert.Fail("Unexpected error"));
        Assert.True(controller.IsRefreshing);
        await controller.RefreshAsync(Fetch, Reload, () => true, _ => Assert.Fail("Unexpected error"));
        Assert.Equal(1, requests);
        controller.Cancel();
        await first;
        pending.SetResult("late");
        Assert.Equal(0, reloads);
        Assert.False(controller.IsRefreshing);
    }
}