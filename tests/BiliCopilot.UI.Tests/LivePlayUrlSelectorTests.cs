using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播播放地址选择：拼出可交给 mpv 的绝对地址，并做最基础的候选排序（完整排序归 #194）。
/// </summary>
public sealed class LivePlayUrlSelectorTests
{
    [Fact]
    public void Select_ComposesAbsoluteHttpsUrlFromSchemeLessHost()
    {
        var line = Line("avc", 400, Url("http_hls", "cn-gotcha.bilivideo.com", "/live-bvc/a.m3u8", "?expires=1&sign=x"));

        var target = LivePlayUrlSelector.Select([line]);

        Assert.NotNull(target);
        Assert.Equal("https://cn-gotcha.bilivideo.com/live-bvc/a.m3u8?expires=1&sign=x", target.Url);
        Assert.Equal("avc", target.CodecName);
        Assert.Equal(400, target.Quality);
        Assert.Equal("http_hls", target.Protocol);
        Assert.False(target.IsMcdn);
    }

    [Fact]
    public void Select_KeepsHostSchemeAndAddsMissingRouteSlash()
    {
        var line = Line("avc", 400, Url("http_hls", "https://a.example", "live/x.m3u8", string.Empty));

        var target = LivePlayUrlSelector.Select([line]);

        Assert.Equal("https://a.example/live/x.m3u8", target!.Url);
    }

    [Fact]
    public void Select_PrefersHlsOverFlv()
    {
        var flv = Line("avc", 400, Url("http_stream", "https://flv.example", "/live/a.flv", "?sign=1"));
        var hls = Line("avc", 400, Url("http_hls", "https://hls.example", "/live/a.m3u8", "?sign=2"));

        var target = LivePlayUrlSelector.Select([flv, hls]);

        Assert.Equal("https://hls.example/live/a.m3u8?sign=2", target!.Url);
    }

    [Fact]
    public void Select_PutsMcdnHostLast()
    {
        var mcdn = Line("avc", 400, Url("http_hls", "https://mcdn.example", "/live/a.m3u8", string.Empty));
        var direct = Line("avc", 400, Url("http_hls", "https://direct.example", "/live/a.m3u8", string.Empty));

        var target = LivePlayUrlSelector.Select([mcdn, direct]);

        Assert.Equal("https://direct.example/live/a.m3u8", target!.Url);
    }

    [Fact]
    public void Select_PrefersAvcCodec()
    {
        var hevc = Line("hevc", 400, Url("http_hls", "https://a.example", "/live/hevc.m3u8", string.Empty));
        var avc = Line("avc", 400, Url("http_hls", "https://a.example", "/live/avc.m3u8", string.Empty));

        var target = LivePlayUrlSelector.Select([hevc, avc]);

        Assert.Equal("avc", target!.CodecName);
    }

    [Fact]
    public void Order_RemovesDuplicatedUrls()
    {
        var first = Line("avc", 400, Url("http_hls", "https://a.example", "/live/a.m3u8", "?sign=1"));
        var duplicate = Line("hevc", 400, Url("http_hls", "https://a.example", "/live/a.m3u8", "?sign=1"));
        var other = Line("avc", 400, Url("http_hls", "https://b.example", "/live/b.m3u8", string.Empty));

        var targets = LivePlayUrlSelector.Order([first, duplicate, other]);

        Assert.Equal(2, targets.Count);
        Assert.Equal("https://a.example/live/a.m3u8?sign=1", targets[0].Url);
        Assert.Equal("https://b.example/live/b.m3u8", targets[1].Url);
    }

    [Fact]
    public void Select_ReturnsNullWhenNoPlayableUrl()
    {
        Assert.Null(LivePlayUrlSelector.Select([]));
        Assert.Null(LivePlayUrlSelector.Select([Line("avc", 400)]));
        Assert.Null(LivePlayUrlSelector.Select(null));
    }


    [Fact]
    public void Order_EmptyAndNullInputsReturnEmpty()
    {
        Assert.Empty(LivePlayUrlSelector.Order(null));
        Assert.Empty(LivePlayUrlSelector.Order([]));
        Assert.Empty(LivePlayUrlSelector.OrderCandidates(null));
        Assert.Empty(LivePlayUrlSelector.OrderCandidates([]));
        Assert.Empty(LivePlayUrlSelector.Order([Line("avc", 400)]));
    }

    [Fact]
    public void OrderCandidates_AllMcdnKeepsOriginalOrderInsteadOfDropping()
    {
        var first = Line("avc", 400, Url("http_hls", "https://mcdn-a.example", "/live/a.m3u8", string.Empty));
        var second = Line("avc", 400, Url("http_hls", "https://mcdn-b.example", "/live/b.m3u8", string.Empty));
        var third = Line("hevc", 400, Url("http_stream", "https://mcdn-c.example", "/live/c.flv", string.Empty));

        var candidates = LivePlayUrlSelector.OrderCandidates([first, second, third]);

        Assert.Equal(3, candidates.Count);
        Assert.Equal("https://mcdn-a.example/live/a.m3u8", candidates[0].Primary.Url);
        Assert.Equal("https://mcdn-b.example/live/b.m3u8", candidates[1].Primary.Url);
        Assert.Equal("https://mcdn-c.example/live/c.flv", candidates[2].Primary.Url);
        Assert.All(candidates, candidate => Assert.True(candidate.IsMcdn));
    }

    [Fact]
    public void Order_RanksFmp4BeforeTsBeforeFlvWithinSameProtocolAndCodec()
    {
        var flv = Line("avc", 400, Url("http_hls", "https://a.example", "/live/a.flv", string.Empty));
        var ts = Line("avc", 400, Url("http_hls", "https://a.example", "/live/b.m3u8", string.Empty));
        var fmp4 = Line("avc", 400, Url("http_hls", "https://a.example", "/live/c_fmp4.m3u8", string.Empty));
        var unknown = Line("avc", 400, Url("http_hls", "https://a.example", "/live/d", string.Empty));

        var targets = LivePlayUrlSelector.Order([flv, ts, unknown, fmp4]);

        Assert.Equal(
            ["https://a.example/live/c_fmp4.m3u8", "https://a.example/live/b.m3u8", "https://a.example/live/a.flv", "https://a.example/live/d"],
            targets.Select(target => target.Url));
        Assert.Equal(
            [LiveContainerKind.Fmp4, LiveContainerKind.Ts, LiveContainerKind.Flv, LiveContainerKind.Unknown],
            targets.Select(target => target.Container));
    }

    [Fact]
    public void DetectContainer_DerivesFromUrlSuffixAndFmp4Marker()
    {
        Assert.Equal(LiveContainerKind.Fmp4, LivePlayUrlSelector.DetectContainer("https://a.example/live/index_fmp4.m3u8?sign=1"));
        Assert.Equal(LiveContainerKind.Fmp4, LivePlayUrlSelector.DetectContainer("https://a.example/live/seg.m4s?sign=1"));
        Assert.Equal(LiveContainerKind.Ts, LivePlayUrlSelector.DetectContainer("https://a.example/live/index.m3u8?sign=1"));
        Assert.Equal(LiveContainerKind.Ts, LivePlayUrlSelector.DetectContainer("https://a.example/live/seg.ts"));
        Assert.Equal(LiveContainerKind.Flv, LivePlayUrlSelector.DetectContainer("https://a.example/live/a.flv?sign=1"));
        Assert.Equal(LiveContainerKind.Unknown, LivePlayUrlSelector.DetectContainer("https://a.example/live/stream"));
        Assert.Equal(LiveContainerKind.Unknown, LivePlayUrlSelector.DetectContainer(null));
    }

    [Fact]
    public void OrderCandidates_MultipleUrlsInsideOneCandidateAreTriedInOriginalOrder()
    {
        var line = Line(
            "avc",
            400,
            Url("http_hls", "https://first.example", "/live/a.m3u8", "?sign=1"),
            Url("http_hls", "https://second.example", "/live/a.m3u8", "?sign=1"),
            Url("http_hls", "https://third.example", "/live/a.m3u8", "?sign=1"));

        var candidate = Assert.Single(LivePlayUrlSelector.OrderCandidates([line]));

        Assert.Equal(
            ["https://first.example/live/a.m3u8?sign=1", "https://second.example/live/a.m3u8?sign=1", "https://third.example/live/a.m3u8?sign=1"],
            candidate.Targets.Select(target => target.Url));
        Assert.Equal(candidate.Targets.Select(target => target.Url), LivePlayUrlSelector.Order([line]).Select(target => target.Url));
    }

    [Fact]
    public void OrderCandidates_McdnCandidateGoesLastEvenWhenItsProtocolOrContainerIsBetter()
    {
        var mcdnFmp4 = Line("avc", 400, Url("http_hls", "https://mcdn.example", "/live/best_fmp4.m3u8", string.Empty));
        var directFlv = Line("hevc", 400, Url("http_stream", "https://direct.example", "/live/worst.flv", string.Empty));

        var candidates = LivePlayUrlSelector.OrderCandidates([mcdnFmp4, directFlv]);

        Assert.Equal("https://direct.example/live/worst.flv", candidates[0].Primary.Url);
        Assert.Equal("https://mcdn.example/live/best_fmp4.m3u8", candidates[1].Primary.Url);
        Assert.True(candidates[1].IsMcdn);
    }

    [Fact]
    public void OrderCandidates_DeduplicatesUrlsWithinAndAcrossCandidates()
    {
        var first = Line(
            "avc",
            400,
            Url("http_hls", "https://a.example", "/live/a.m3u8", "?sign=1"),
            Url("http_hls", "https://a.example", "/live/a.m3u8", "?sign=1"));
        var second = Line("hevc", 400, Url("http_hls", "https://a.example", "/live/a.m3u8", "?sign=1"));

        var candidates = LivePlayUrlSelector.OrderCandidates([first, second]);

        var candidate = Assert.Single(candidates);
        Assert.Single(candidate.Targets);
        Assert.Equal("a.example", candidate.Primary.Host);
    }

    private static LiveLineInformation Line(string codec, int quality, params LivePlayUrl[] urls)
        => new(codec, quality, [quality], urls);

    private static LivePlayUrl Url(string protocol, string host, string route, string query)
        => new(protocol, host, route, query);
}