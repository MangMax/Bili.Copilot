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

    private static DashSegmentInformation Segment(string id, string url, IList<string> backups)
        => new(id, url, 100, "", "avc", 0, 0, "", "", backups);

    public class PlaybackService : DispatchProxy
    {
        public DashMediaInformation Information { get; set; } = null!;
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name is "GetVideoPlayDetailAsync" or "GetPgcPlayDetailAsync")
            {
                Calls++;
                return Task.FromResult(Information);
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
