using System.Reflection;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Resolvers;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

public sealed class PlaybackRoutingResolverTests
{
    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(true, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(true, 1, false)]
    [InlineData(false, 1, true)]
    [InlineData(true, 1, true)]
    [InlineData(false, 2, false)]
    [InlineData(true, 2, false)]
    [InlineData(false, 3, false)]
    [InlineData(true, 3, false)]
    [InlineData(false, 4, false)]
    [InlineData(true, 4, false)]
    [InlineData(false, 5, false)]
    [InlineData(true, 5, false)]
    [InlineData(false, 6, false)]
    [InlineData(true, 6, false)]
    public async Task Source_SelectsVideoAndAudioIndependently(bool pgc, int preference, bool withoutP2P)
    {
        NavigationTestServices.Settings.Value = new()
        {
            [SettingNames.PlaybackRoutePreference] = preference,
            [SettingNames.PlayWithoutP2P] = withoutP2P,
        };
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        const string video = "https://upos-sz-mirrorali.bilivideo.com/v?sign=Video%2f";
        const string audio = "https://upos-sz-mirrorcos.bilivideo.com/a?sign=Audio%2f";
        var service = DispatchProxy.Create<IPlayerService, PlaybackService>();
        ((PlaybackService)(object)service).Information = new(
            [Segment("80", "https://a.mountaintoys.cn/v", [video])],
            [Segment("30280", "https://a.szbdyd.com/a", [audio])],
            [new(80, "1080P", false, false)]);
        var cookies = DispatchProxy.Create<IBiliCookiesResolver, CookieService>();
        var snapshot = new MediaSnapshot(new VideoInformation(new("video", "Title", null), null!));
        MediaSourceResolverBase resolver;
        if (pgc)
        {
            snapshot.Episode = new(new("episode", "Episode", null));
            var source = new PgcMediaSourceResolver(service, cookies, NullLogger<PgcMediaSourceResolver>.Instance);
            source.Initialize(snapshot, snapshot.Episode);
            resolver = source;
        }
        else
        {
            var source = new VideoMediaSourceResolver(service, cookies, NullLogger<VideoMediaSourceResolver>.Instance);
            source.Initialize(snapshot, new(new("1", "Part", null), 1, 60));
            resolver = source;
        }

        var result = await resolver.GetSourceAsync();

        Assert.Equal(preference == 1 && !withoutP2P ? "https://a.mountaintoys.cn/v" : video, result.Url);
        Assert.Equal(preference == 1 && !withoutP2P ? "https://a.szbdyd.com/a" : audio, Assert.Single(result.Options.AudioTracks!));
        Assert.Equal("cookie", result.Options.HttpHeaders!["Cookie"]);
        Assert.Equal(1, ((PlaybackService)(object)service).Calls);
        Assert.DoesNotContain(NavigationTestServices.Settings.Value.Values, value => value is string text && text.Contains("sign="));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_FetchesAgainAndAvoidsPreviousTrackHosts(bool pgc)
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var service = DispatchProxy.Create<IPlayerService, PlaybackService>();
        var api = (PlaybackService)(object)service;
        api.Information = new(
            [Segment("80", "https://video.example/v1", ["https://other-video.example/v2"])],
            [Segment("30280", "https://audio.example/a1", ["https://other-audio.example/a2"])],
            [new(80, "1080P", false, false)]);
        var resolver = CreateResolver(pgc, service);
        var old = await resolver.GetSourceAsync();
        Assert.Equal("https://video.example/v1", old.Url);
        var refreshed = await resolver.RefreshSourceAsync(CancellationToken.None);
        Assert.Equal(2, api.Calls);
        Assert.Equal("https://other-video.example/v2", refreshed.Url);
        Assert.Equal("https://other-audio.example/a2", Assert.Single(refreshed.Options.AudioTracks!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initialize_RejectsLateOldFetchAndKeepsNewMediaCache(bool pgc)
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var service = DispatchProxy.Create<IPlayerService, PlaybackService>();
        var api = (PlaybackService)(object)service;
        var late = new TaskCompletionSource<DashMediaInformation>();
        api.Fetch = () => late.Task;
        var resolver = CreateResolver(pgc, service);
        var old = resolver.GetSourceAsync();
        var snapshot = new MediaSnapshot(new VideoInformation(new("new", "New", null), null!));
        if (resolver is VideoMediaSourceResolver video)
            video.Initialize(snapshot, new(new("2", "New part", null), 2, 60));
        else
        {
            snapshot.Episode = new(new("new", "New episode", null));
            ((PgcMediaSourceResolver)resolver).Initialize(snapshot, snapshot.Episode);
        }
        var current = new DashMediaInformation([Segment("80", "https://new.example/v", [])], null!, [new(80, "1080P", false, false)]);
        api.Fetch = () => Task.FromResult(current);
        Assert.Equal("https://new.example/v", (await resolver.GetSourceAsync()).Url);
        late.SetResult(new([Segment("80", "https://old.example/v", [])], null!, [new(80, "1080P", false, false)]));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old);
        Assert.Equal("https://new.example/v", (await resolver.GetSourceAsync()).Url);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_UnavailableQualityOrFetchFailurePreservesPlayableSource(bool pgc)
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var service = DispatchProxy.Create<IPlayerService, PlaybackService>();
        var api = (PlaybackService)(object)service;
        api.Information = new([Segment("80", "https://old.example/v", [])], null!, [new(80, "1080P", false, false)]);
        var resolver = CreateResolver(pgc, service);
        await resolver.GetSourceAsync();
        api.Information = new([Segment("64", "https://lower.example/v", [])], null!, [new(64, "720P", false, false)]);
        await Assert.ThrowsAnyAsync<Exception>(() => resolver.RefreshSourceAsync(CancellationToken.None));
        Assert.Equal("https://old.example/v", (await resolver.GetSourceAsync()).Url);
        api.Fetch = () => Task.FromException<DashMediaInformation>(new InvalidOperationException("Network failed"));
        await Assert.ThrowsAnyAsync<Exception>(() => resolver.RefreshSourceAsync(CancellationToken.None));
        Assert.Equal("https://old.example/v", (await resolver.GetSourceAsync()).Url);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_RetainsActualSelectedCodecAndRejectsUnavailableCodec(bool pgc)
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var api = DispatchProxy.Create<IPlayerService, PlaybackService>();
        var service = (PlaybackService)(object)api;
        service.Information = new([Segment("80", "https://old.example/v", [])], null!, [new(80, "1080P", false, false)]);
        var resolver = CreateResolver(pgc, api);
        await resolver.GetSourceAsync();
        service.Information = new([new("80", "https://hevc.example/v", 100, "", "hev", 0, 0, "", "", [])], null!, [new(80, "1080P", false, false)]);
        await Assert.ThrowsAnyAsync<Exception>(() => resolver.RefreshSourceAsync(CancellationToken.None));
        Assert.Equal("https://old.example/v", (await resolver.GetSourceAsync()).Url);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resolver_CancelledRefreshCannotCommitAndInitialFailureCanRetry(bool pgc)
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var api = DispatchProxy.Create<IPlayerService, PlaybackService>();
        var service = (PlaybackService)(object)api;
        service.Information = new([Segment("80", "https://old.example/v", [])], null!, [new(80, "1080P", false, false)]);
        service.Fetch = () => Task.FromException<DashMediaInformation>(new InvalidOperationException("network"));
        var resolver = CreateResolver(pgc, api);
        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.GetSourceAsync());
        service.Fetch = null;
        Assert.Equal("https://old.example/v", (await resolver.GetSourceAsync()).Url);
        var delayed = new TaskCompletionSource<DashMediaInformation>();
        service.Fetch = () => delayed.Task;
        using var cancellation = new CancellationTokenSource();
        var refresh = resolver.RefreshSourceAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        delayed.SetResult(new([Segment("80", "https://late.example/v", [])], null!, [new(80, "1080P", false, false)]));
        Assert.Equal("https://old.example/v", (await resolver.GetSourceAsync()).Url);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_NewerQualitySelectionRejectsLateSource(bool pgc)
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var api = DispatchProxy.Create<IPlayerService, PlaybackService>();
        var service = (PlaybackService)(object)api;
        service.Information = new([Segment("80", "https://old.example/high", []), Segment("64", "https://old.example/low", [])], null!, [new(80, "1080P", false, false), new(64, "720P", false, false)]);
        var resolver = CreateResolver(pgc, api);
        await resolver.GetSourceAsync();
        var delayed = new TaskCompletionSource<DashMediaInformation>();
        service.Fetch = () => delayed.Task;
        var refresh = resolver.RefreshSourceAsync(CancellationToken.None);
        var snapshot = new MediaSnapshot(new VideoInformation(new("video", "Title", null), null!)) { PreferQuality = 64 };
        if (resolver is VideoMediaSourceResolver video) video.ResetSnapshot(snapshot);
        else
        {
            snapshot.Episode = new(new("episode", "Episode", null));
            ((PgcMediaSourceResolver)resolver).ResetSnapshot(snapshot);
        }
        delayed.SetResult(new([Segment("80", "https://late.example/high", [])], null!, [new(80, "1080P", false, false)]));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        Assert.Equal("https://old.example/low", (await resolver.GetSourceAsync()).Url);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resolver_ReinitializeAfterSuccessFetchesNewPart(bool pgc)
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new() { [typeof(AccountViewModel)] = new AccountViewModel() };
        var api = DispatchProxy.Create<IPlayerService, PlaybackService>();
        var service = (PlaybackService)(object)api;
        service.Information = new([Segment("80", "https://old.example/v", [])], null!, [new(80, "1080P", false, false)]);
        var resolver = CreateResolver(pgc, api);
        await resolver.GetSourceAsync();
        var snapshot = new MediaSnapshot(new VideoInformation(new("new", "New", null), null!));
        if (resolver is VideoMediaSourceResolver video) video.Initialize(snapshot, new(new("2", "Part", null), 2, 60));
        else
        {
            snapshot.Episode = new(new("new", "New", null));
            ((PgcMediaSourceResolver)resolver).Initialize(snapshot, snapshot.Episode);
        }
        service.Information = new([Segment("80", "https://new.example/v", [])], null!, [new(80, "1080P", false, false)]);
        Assert.Equal("https://new.example/v", (await resolver.GetSourceAsync()).Url);
        Assert.Equal(2, service.Calls);
    }

    internal static MediaSourceResolverBase CreateResolver(bool pgc, IPlayerService service)
    {
        var cookies = DispatchProxy.Create<IBiliCookiesResolver, CookieService>();
        var snapshot = new MediaSnapshot(new VideoInformation(new("video", "Title", null), null!));
        if (pgc)
        {
            snapshot.Episode = new(new("episode", "Episode", null));
            var resolver = new PgcMediaSourceResolver(service, cookies, NullLogger<PgcMediaSourceResolver>.Instance);
            resolver.Initialize(snapshot, snapshot.Episode);
            return resolver;
        }
        var video = new VideoMediaSourceResolver(service, cookies, NullLogger<VideoMediaSourceResolver>.Instance);
        video.Initialize(snapshot, new(new("1", "Part", null), 1, 60));
        return video;
    }

    private static DashSegmentInformation Segment(string id, string url, IList<string> backups)
        => new(id, url, 100, "", "avc", 0, 0, "", "", backups);

    public class PlaybackService : DispatchProxy
    {
        public DashMediaInformation Information { get; set; } = null!;
        public int Calls { get; private set; }
        public Func<Task<DashMediaInformation>>? Fetch { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name is "GetVideoPlayDetailAsync" or "GetPgcPlayDetailAsync")
            {
                Calls++;
                return Fetch?.Invoke() ?? Task.FromResult(Information);
            }
            throw new NotSupportedException(method?.Name);
        }
    }

    public class CookieService : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "GetCookieString" ? "cookie" : throw new NotSupportedException();
    }
}
