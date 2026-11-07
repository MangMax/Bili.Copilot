using System.Net;
using System.Reflection;
using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Resolvers;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播媒体源解析：用内核的直播间详情 + 播放详情拼出 mpv 媒体源，
/// 并带上登录 Cookie、直播 Referer 与统一 UA；未登录时仍能拿到游客地址。
/// </summary>
public sealed class LiveMediaSourceResolverTests
{
    public LiveMediaSourceResolverTests()
    {
        NavigationTestServices.Settings.Value = new();
    }

    [Fact]
    public async Task GetSource_UsesPageAndPlayDetailAndLiveHeaders()
    {
        var service = CreateService();
        var resolver = new LiveMediaSourceResolver(service, Cookie("SESSDATA=token"), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));

        var source = await resolver.GetSourceAsync();

        Assert.Equal("https://hls.example/live/a.m3u8?sign=1", source.Url);
        Assert.Equal("123", source.Id);
        Assert.Equal("房间详情标题", source.Title);
        Assert.Equal("房间详情标题", source.Options.MediaName);
        Assert.Equal(InternalHttpExtensions.LiveReferer, source.Options.HttpHeaders!["Referer"]);
        Assert.Equal("SESSDATA=token", source.Options.HttpHeaders!["Cookie"]);
        Assert.Equal(InternalHttpExtensions.VideoUserAgent, source.Options.UserAgent);
        Assert.True(source.Options.EnableCookies);
        Assert.Equal(1d, source.Options.InitialSpeed);
        Assert.Equal(1, ((LivePlayerService)(object)service).PageCalls);
        Assert.Equal(1, ((LivePlayerService)(object)service).PlayCalls);
        Assert.Equal(400, ((LivePlayerService)(object)service).RequestedQuality);
    }

    [Fact]
    public async Task GetSource_UnloggedStillPlaysWithoutEmptyCookieHeader()
    {
        var service = CreateService();
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));

        var source = await resolver.GetSourceAsync();

        Assert.Equal("https://hls.example/live/a.m3u8?sign=1", source.Url);
        Assert.False(source.Options.HttpHeaders!.ContainsKey("Cookie"));
        Assert.Equal(InternalHttpExtensions.LiveReferer, source.Options.HttpHeaders["Referer"]);
    }

    [Fact]
    public async Task GetSource_UsesPreferredQualityForPlayDetail()
    {
        var service = CreateService();
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))) { PreferQuality = 250 });

        await resolver.GetSourceAsync();

        Assert.Equal(250, ((LivePlayerService)(object)service).RequestedQuality);
    }

    [Fact]
    public async Task GetSource_PrefersHlsAndAvoidsMcdn()
    {
        var service = CreateService(
            [Line("avc", 400, Url("http_stream", "https://mcdn.example", "/live/flv.flv", string.Empty)),
             Line("avc", 400, Url("http_hls", "https://mcdn.example", "/live/hls.m3u8", string.Empty)),
             Line("avc", 400, Url("http_hls", "https://direct.example", "/live/best.m3u8", "?sign=2"))]);
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));

        var source = await resolver.GetSourceAsync();

        Assert.Equal("https://direct.example/live/best.m3u8?sign=2", source.Url);
    }

    [Fact]
    public async Task GetSource_ThrowsWhenNoLineIsPlayable()
    {
        var service = CreateService([]);
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.GetSourceAsync());
    }

    [Fact]
    public async Task Cancel_RejectsLatePlayUrlAndAllowsRetry()
    {
        var service = CreateService();
        var pending = new TaskCompletionSource<LiveMediaInformation>();
        ((LivePlayerService)(object)service).Fetch = () => pending.Task;
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));

        var request = resolver.GetSourceAsync();
        resolver.Cancel();
        pending.SetResult(Playback([Line("avc", 400, Url("http_hls", "https://late.example", "/late.m3u8", string.Empty))]));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);

        ((LivePlayerService)(object)service).Fetch = null;
        var retried = await resolver.GetSourceAsync();
        Assert.Equal("https://hls.example/live/a.m3u8?sign=1", retried.Url);
    }

    [Fact]
    public async Task TryPrepare_ReportsFailureKindWithoutThrowing()
    {
        var service = CreateService();
        ((LivePlayerService)(object)service).Fetch = () => Task.FromException<LiveMediaInformation>(
            new HttpRequestException("403", null, HttpStatusCode.Forbidden));
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));

        var prepared = await resolver.TryPrepareAsync();

        Assert.False(prepared);
        Assert.Equal(LiveFailureKind.AuthenticationFailed, resolver.FailureKind);
    }

    [Fact]
    public async Task GetSource_MapsTransportErrorsToFailureKind()
    {
        var service = CreateService();
        ((LivePlayerService)(object)service).Fetch = () => Task.FromException<LiveMediaInformation>(
            new HttpRequestException("403", null, HttpStatusCode.Forbidden));
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => resolver.GetSourceAsync());

        Assert.Equal(LiveFailureKind.AuthenticationFailed, LiveFailureClassifier.Classify(exception));
    }


    [Fact]
    public async Task Initialize_ReadsPersistedLiveQuality()
    {
        NavigationTestServices.Settings.Value![SettingNames.LastSelectedLiveQuality] = 250;
        var service = CreateService();
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));

        await resolver.GetSourceAsync();

        Assert.Equal(250, resolver.SelectedQuality);
        Assert.Equal(250, ((LivePlayerService)(object)service).RequestedQuality);
    }

    [Fact]
    public async Task SelectQuality_PersistsOnlyTheQualityIdAndRefreshesWithIt()
    {
        var service = CreateService();
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))) { PreferQuality = 400 });
        await resolver.GetSourceAsync();

        resolver.SelectQuality(150);
        await resolver.RefreshSourceAsync(CancellationToken.None);

        Assert.Equal(150, (int)NavigationTestServices.Settings.Value![SettingNames.LastSelectedLiveQuality]!);
        Assert.DoesNotContain(
            NavigationTestServices.Settings.Value.Values,
            value => value is string text && (text.Contains("sign=", StringComparison.Ordinal) || text.Contains("https://", StringComparison.Ordinal)));
        Assert.Equal(150, ((LivePlayerService)(object)service).RequestedQuality);
    }

    [Fact]
    public async Task RefreshSource_RequestsFreshSignedUrlWithoutPositionOrSpeedInheritance()
    {
        var service = CreateService();
        var calls = 0;
        ((LivePlayerService)(object)service).Fetch = () => Task.FromResult(Playback(
            [Line("avc", 400, Url("http_hls", "https://hls.example", "/live/a.m3u8", "?sign=" + (++calls)))]));
        var snapshot = new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null)))
        {
            StartPosition = 42,
            PreferQuality = 400,
        };
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(snapshot);
        var first = await resolver.GetSourceAsync();

        var refreshed = await resolver.RefreshSourceAsync(CancellationToken.None);

        Assert.Equal("https://hls.example/live/a.m3u8?sign=1", first.Url);
        Assert.Equal("https://hls.example/live/a.m3u8?sign=2", refreshed.Url);
        Assert.Null(refreshed.Options.StartPosition);
        Assert.Equal(1d, refreshed.Options.InitialSpeed);
        Assert.Equal(2, ((LivePlayerService)(object)service).PageCalls);
        Assert.Equal(2, ((LivePlayerService)(object)service).PlayCalls);
    }

    [Fact]
    public async Task Recovery_AdvancesWithinTheSameCandidateBeforeReRequestingPlayUrl()
    {
        var service = CreateService(
            [Line("avc", 400,
                Url("http_hls", "https://first.example", "/live/a.m3u8", "?sign=1"),
                Url("http_hls", "https://second.example", "/live/a.m3u8", "?sign=1"))]);
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));
        var first = await resolver.GetSourceAsync();

        Assert.Equal(LiveRecoveryAction.AdvanceCandidate, resolver.NotifyPlaybackFailure(LivePlaybackFailureKind.Transport));
        var next = await resolver.GetRecoverySourceAsync(CancellationToken.None);

        Assert.Equal("https://first.example/live/a.m3u8?sign=1", first.Url);
        Assert.Equal("https://second.example/live/a.m3u8?sign=1", next.Url);
        Assert.Equal(1, ((LivePlayerService)(object)service).PlayCalls);

        Assert.Equal(LiveRecoveryAction.ReRequestPlayUrl, resolver.NotifyPlaybackFailure(LivePlaybackFailureKind.Transport));
    }

    [Fact]
    public async Task Recovery_ReRequestsPlayUrlWhenCandidatesAreExhaustedAndThenGivesManualRetry()
    {
        var service = CreateService();
        var calls = 0;
        ((LivePlayerService)(object)service).Fetch = () => Task.FromResult(Playback(
            [Line("avc", 400, Url("http_hls", "https://hls.example", "/live/a.m3u8", "?sign=" + (++calls)))]));
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));
        await resolver.GetSourceAsync();

        Assert.Equal(LiveRecoveryAction.ReRequestPlayUrl, resolver.NotifyPlaybackFailure(LivePlaybackFailureKind.DecoderInit));
        var next = await resolver.GetRecoverySourceAsync(CancellationToken.None);

        Assert.Equal("https://hls.example/live/a.m3u8?sign=2", next.Url);
        Assert.Equal(2, ((LivePlayerService)(object)service).PlayCalls);
        Assert.Equal(LiveRecoveryAction.ManualRetry, resolver.NotifyPlaybackFailure(LivePlaybackFailureKind.Transport));
        await Assert.ThrowsAsync<LivePlaybackUnavailableException>(() => resolver.GetRecoverySourceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Recovery_IgnoresEchoedDecoderFailureWithinOneAttempt()
    {
        var service = CreateService(
            [Line("avc", 400,
                Url("http_hls", "https://first.example", "/live/a.m3u8", string.Empty),
                Url("http_hls", "https://second.example", "/live/a.m3u8", string.Empty))]);
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));
        await resolver.GetSourceAsync();

        Assert.Equal(LiveRecoveryAction.AdvanceCandidate, resolver.NotifyPlaybackFailure(LivePlaybackFailureKind.Transport));
        Assert.Equal(LiveRecoveryAction.None, resolver.NotifyPlaybackFailure(LivePlaybackFailureKind.DecoderInit));
    }

    [Fact]
    public async Task Refresh_KeepsLinePreferenceAcrossNewSignedUrls()
    {
        var service = CreateService(
            [Line("avc", 400, Url("http_hls", "https://first.example", "/live/a.m3u8", "?sign=1")),
             Line("avc", 400, Url("http_hls", "https://second.example", "/live/a.m3u8", "?sign=1"))]);
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));
        await resolver.GetSourceAsync();
        var preferred = resolver.Candidates.Single(candidate => candidate.Primary.Host == "second.example");
        resolver.SelectLine(preferred.Key);

        var calls = 0;
        ((LivePlayerService)(object)service).Fetch = () => Task.FromResult(Playback(
            [Line("avc", 400, Url("http_hls", "https://first.example", "/live/a.m3u8", "?sign=" + (++calls))),
             Line("avc", 400, Url("http_hls", "https://second.example", "/live/a.m3u8", "?sign=" + calls))]));
        var refreshed = await resolver.RefreshSourceAsync(CancellationToken.None);

        Assert.Equal("second.example", new Uri(refreshed.Url).Host);
        Assert.Equal("second.example", resolver.CurrentCandidate!.Primary.Host);
    }

    [Fact]
    public async Task Cancel_CancelsInFlightRefreshAndRejectsLateResult()
    {
        var service = CreateService();
        var pending = new TaskCompletionSource<LiveMediaInformation>();
        var resolver = new LiveMediaSourceResolver(service, Cookie(string.Empty), NullLogger<LiveMediaSourceResolver>.Instance);
        resolver.Initialize(new MediaSnapshot(new LiveInformation(new MediaIdentifier("123", "卡片标题", null))));
        ((LivePlayerService)(object)service).Fetch = () => Task.FromResult(Playback(
            [Line("avc", 400, Url("http_hls", "https://hls.example", "/live/a.m3u8", "?sign=1"))]));
        await resolver.GetSourceAsync();
        ((LivePlayerService)(object)service).Fetch = () => pending.Task;

        var refresh = resolver.RefreshSourceAsync(CancellationToken.None);
        resolver.Cancel();
        pending.SetResult(Playback([Line("avc", 400, Url("http_hls", "https://late.example", "/live/a.m3u8", "?sign=9"))]));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        ((LivePlayerService)(object)service).Fetch = null;
        Assert.Equal("https://hls.example/live/a.m3u8?sign=1", (await resolver.GetSourceAsync()).Url);
    }

    private static IPlayerService CreateService(IList<LiveLineInformation>? lines = null)
    {
        var service = DispatchProxy.Create<IPlayerService, LivePlayerService>();
        var api = (LivePlayerService)(object)service;
        api.View = Page();
        api.Information = Playback(lines ?? [Line("avc", 400, Url("http_hls", "https://hls.example", "/live/a.m3u8", "?sign=1"))]);
        return service;
    }

    private static IBiliCookiesResolver Cookie(string value)
    {
        var cookies = DispatchProxy.Create<IBiliCookiesResolver, CookieService>();
        ((CookieService)(object)cookies).Value = value;
        return cookies;
    }

    private static LivePlayerView Page()
    {
        var info = new LiveInformation(
            new MediaIdentifier("123", "房间详情标题", null),
            new UserProfile("anchor", "主播", null));
        info.AddExtensionIfNotNull(LiveExtensionDataId.IsLiving, true);
        return new LivePlayerView(info, new LiveTag("area", "分区", "default"));
    }

    private static LiveMediaInformation Playback(IList<LiveLineInformation> lines)
        => new("123", [new PlayerFormatInformation(400, "蓝光", false, false)], lines);

    private static LiveLineInformation Line(string codec, int quality, params LivePlayUrl[] urls)
        => new(codec, quality, [quality], urls);

    private static LivePlayUrl Url(string protocol, string host, string route, string query)
        => new(protocol, host, route, query);

    public class LivePlayerService : DispatchProxy
    {
        public LivePlayerView View { get; set; } = null!;

        public LiveMediaInformation Information { get; set; } = null!;

        public Func<Task<LiveMediaInformation>>? Fetch { get; set; }

        public int PageCalls { get; private set; }

        public int PlayCalls { get; private set; }

        public int RequestedQuality { get; private set; }

        public List<int> RequestedQualities { get; } = [];

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method?.Name)
            {
                case "GetLivePageDetailAsync":
                    PageCalls++;
                    return Task.FromResult(View);
                case "GetLivePlayDetailAsync":
                    PlayCalls++;
                    RequestedQuality = args is { Length: > 1 } ? (int)args[1]! : 0;
                    RequestedQualities.Add(RequestedQuality);
                    return Fetch?.Invoke() ?? Task.FromResult(Information);
                default:
                    throw new NotSupportedException(method?.Name);
            }
        }
    }

    public class CookieService : DispatchProxy
    {
        public string Value { get; set; } = "cookie";

        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "GetCookieString" ? Value : throw new NotSupportedException();
    }
}