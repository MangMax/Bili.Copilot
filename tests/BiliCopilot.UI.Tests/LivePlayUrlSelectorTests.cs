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

    private static LiveLineInformation Line(string codec, int quality, params LivePlayUrl[] urls)
        => new(codec, quality, [quality], urls);

    private static LivePlayUrl Url(string protocol, string host, string route, string query)
        => new(protocol, host, route, query);
}
