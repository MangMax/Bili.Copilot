using BiliCopilot.UI.Live;
using BiliCopilot.UI.Tests.Support;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 内核直播事件到房间状态处理类别的映射：只认房间相关事件，弹幕与连接状态不进入状态链路。
/// </summary>
public sealed class LiveRoomEventMapperTests
{
    [Fact]
    public void MapsRoomLifecycleEvents()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(LiveRoomStatusSignal.Started, LiveRoomEventMapper.Map(new LiveStartedEvent("LIVE", now)).Signal);
        Assert.Equal(LiveRoomStatusSignal.NotStarted, LiveRoomEventMapper.Map(new LiveRoomNotStartedEvent("PREPARING", now)).Signal);

        var cutOff = LiveRoomEventMapper.Map(new LiveRoomCutOffEvent("被管理员切断", "CUT_OFF", now));
        Assert.Equal(LiveRoomStatusSignal.CutOff, cutOff.Signal);
        Assert.Equal("被管理员切断", cutOff.Detail);
    }

    [Fact]
    public void MapsTitleChangeWithArea()
    {
        var update = LiveRoomEventMapper.Map(new LiveRoomTitleChangedEvent("新标题", "虚拟主播", "ROOM_CHANGE", DateTimeOffset.UtcNow));

        Assert.Equal(LiveRoomStatusSignal.TitleChanged, update.Signal);
        Assert.Equal("新标题", update.Title);
        Assert.Equal("虚拟主播", update.AreaName);
    }

    [Fact]
    public void MapsOnlineCountWithSource()
    {
        var watched = LiveRoomEventMapper.Map(new LiveOnlineCountChangedEvent(1234, LiveOnlineCountSource.Watched, "1234", "WATCHED_CHANGE", DateTimeOffset.UtcNow));
        Assert.Equal(LiveRoomStatusSignal.OnlineCountChanged, watched.Signal);
        Assert.Equal(1234, watched.OnlineCount);
        Assert.Equal(LiveOnlineCountSource.Watched, watched.OnlineCountSource);

        var popularity = LiveRoomEventMapper.Map(new LiveOnlineCountChangedEvent(9000, LiveOnlineCountSource.Popularity, null, "HEARTBEAT", DateTimeOffset.UtcNow));
        Assert.Equal(LiveOnlineCountSource.Popularity, popularity.OnlineCountSource);
    }

    [Fact]
    public void MapsPlayUrlReload()
        => Assert.Equal(LiveRoomStatusSignal.PlayUrlReload, LiveRoomEventMapper.Map(new LivePlayUrlReloadEvent("{}", "PLAYURL_RELOAD", DateTimeOffset.UtcNow)).Signal);

    [Fact]
    public void IgnoresDanmakuAndConnectionEvents()
    {
        Assert.Equal(LiveRoomStatusSignal.None, LiveRoomEventMapper.Map(TestLiveEvents.Danmaku("hello")).Signal);
        Assert.Equal(LiveRoomStatusSignal.None, LiveRoomEventMapper.Map(TestLiveEvents.SuperChat("sc")).Signal);
        Assert.Equal(LiveRoomStatusSignal.None, LiveRoomEventMapper.Map(TestLiveEvents.ConnectionState(LiveDanmakuConnectionState.Connected)).Signal);
        Assert.Equal(LiveRoomStatusSignal.None, LiveRoomEventMapper.Map(null).Signal);
    }
}
