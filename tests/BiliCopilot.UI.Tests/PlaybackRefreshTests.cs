using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.PlaybackRouting;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Components;
using Richasy.BiliKernel.Bili.Media;
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
    public async Task PlayerRefresh_NewPauseOrSwitchIntentDuringFetchPreventsReload(bool switching)
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
