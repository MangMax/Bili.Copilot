// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 房间事件在界面上的处理类别。
/// </summary>
public enum LiveRoomStatusSignal
{
    /// <summary>
    /// 与房间状态无关（弹幕、醒目留言、连接状态等）.
    /// </summary>
    None,

    /// <summary>
    /// 主播开播.
    /// </summary>
    Started,

    /// <summary>
    /// 主播已下播.
    /// </summary>
    NotStarted,

    /// <summary>
    /// 直播被切断.
    /// </summary>
    CutOff,

    /// <summary>
    /// 房间标题变更.
    /// </summary>
    TitleChanged,

    /// <summary>
    /// 在线人数变化.
    /// </summary>
    OnlineCountChanged,

    /// <summary>
    /// 播放地址需要重载.
    /// </summary>
    PlayUrlReload,
}

/// <summary>
/// 一条房间事件归一化后的结果。
/// </summary>
/// <param name="Signal">处理类别.</param>
/// <param name="Title">新标题（仅标题变更时有值）.</param>
/// <param name="AreaName">分区名称（仅标题变更时有值）.</param>
/// <param name="OnlineCount">人数或人气值（仅人数变化时有值）.</param>
/// <param name="OnlineCountSource">人数来源.</param>
/// <param name="Detail">服务端提示（仅被切断时有值）.</param>
public readonly record struct LiveRoomStatusUpdate(
    LiveRoomStatusSignal Signal,
    string? Title = null,
    string? AreaName = null,
    int OnlineCount = 0,
    LiveOnlineCountSource OnlineCountSource = LiveOnlineCountSource.Popularity,
    string? Detail = null)
{
    /// <summary>
    /// Gets 无需处理的结果.
    /// </summary>
    public static LiveRoomStatusUpdate None => new(LiveRoomStatusSignal.None);
}

/// <summary>
/// 把内核直播事件映射成房间状态处理类别。
/// </summary>
/// <remarks>
/// 纯函数：只做类型分派与字段搬运，不触碰界面与播放器，因此可直接单测。
/// 弹幕、醒目留言与连接状态返回 <see cref="LiveRoomStatusSignal.None"/>，
/// 避免它们进入房间状态链路造成额外调度。
/// </remarks>
public static class LiveRoomEventMapper
{
    /// <summary>
    /// 映射一条直播事件。
    /// </summary>
    /// <param name="liveEvent">直播事件.</param>
    /// <returns>归一化结果；无关事件返回 <see cref="LiveRoomStatusUpdate.None"/>.</returns>
    public static LiveRoomStatusUpdate Map(LiveDanmakuEvent? liveEvent)
        => liveEvent switch
        {
            LiveStartedEvent => new(LiveRoomStatusSignal.Started),
            LiveRoomNotStartedEvent => new(LiveRoomStatusSignal.NotStarted),
            LiveRoomCutOffEvent cutOff => new(LiveRoomStatusSignal.CutOff, Detail: cutOff.Message),
            LiveRoomTitleChangedEvent title => new(LiveRoomStatusSignal.TitleChanged, title.Title, title.AreaName),
            LiveOnlineCountChangedEvent online => new(
                LiveRoomStatusSignal.OnlineCountChanged,
                OnlineCount: online.Count,
                OnlineCountSource: online.Source),
            LivePlayUrlReloadEvent => new(LiveRoomStatusSignal.PlayUrlReload),
            _ => LiveRoomStatusUpdate.None,
        };
}
