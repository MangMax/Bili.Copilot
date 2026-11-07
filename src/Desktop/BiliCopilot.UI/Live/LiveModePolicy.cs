// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播模式下不可用的时间轴能力。
/// </summary>
/// <param name="SupportsTimeline">是否支持时间轴（进度、拖拽、结束判定等）.</param>
/// <param name="ReportsProgress">是否上报观看进度.</param>
/// <param name="SavesHistory">是否写入观看历史.</param>
/// <param name="ResumesPlayback">是否从上次位置续播.</param>
/// <param name="DetectsEnd">是否判定播放结束.</param>
/// <param name="AutoPlaysNext">是否自动播放下一集.</param>
/// <param name="SupportsChapters">是否支持章节.</param>
/// <param name="SupportsSubtitles">是否支持字幕.</param>
/// <param name="SupportsSpeed">是否支持倍速.</param>
/// <param name="RunsSponsorBlock">是否运行空降助手.</param>
/// <param name="SupportsDanmakuTimeline">是否按时间轴分发弹幕.</param>
public readonly record struct LiveModeCapabilities(
    bool SupportsTimeline,
    bool ReportsProgress,
    bool SavesHistory,
    bool ResumesPlayback,
    bool DetectsEnd,
    bool AutoPlaysNext,
    bool SupportsChapters,
    bool SupportsSubtitles,
    bool SupportsSpeed,
    bool RunsSponsorBlock,
    bool SupportsDanmakuTimeline);

/// <summary>
/// 直播模式判定：直播没有时长与终点，因此不进入任何依赖时间轴的播放路径。
/// </summary>
/// <remarks>
/// 这里是播放器与界面共用的唯一判定来源，避免在 Position / Duration / 历史 / 章节等
/// 多个分支各自猜测"当前是不是直播"。
/// </remarks>
public static class LiveModePolicy
{
    private static readonly LiveModeCapabilities LiveCapabilities = new(
        SupportsTimeline: false,
        ReportsProgress: false,
        SavesHistory: false,
        ResumesPlayback: false,
        DetectsEnd: false,
        AutoPlaysNext: false,
        SupportsChapters: false,
        SupportsSubtitles: false,
        SupportsSpeed: false,
        RunsSponsorBlock: false,
        SupportsDanmakuTimeline: false);

    private static readonly LiveModeCapabilities TimelineCapabilities = new(
        SupportsTimeline: true,
        ReportsProgress: true,
        SavesHistory: true,
        ResumesPlayback: true,
        DetectsEnd: true,
        AutoPlaysNext: true,
        SupportsChapters: true,
        SupportsSubtitles: true,
        SupportsSpeed: true,
        RunsSponsorBlock: true,
        SupportsDanmakuTimeline: true);

    /// <summary>
    /// 是否为直播媒体。
    /// </summary>
    /// <param name="type">媒体类型.</param>
    /// <returns>直播时为 true.</returns>
    public static bool IsLive(BiliMediaType type) => type == BiliMediaType.Live;

    /// <summary>
    /// 获取该媒体类型可用的播放能力。
    /// </summary>
    /// <param name="type">媒体类型.</param>
    /// <returns>播放能力集合.</returns>
    public static LiveModeCapabilities For(BiliMediaType type)
        => type == BiliMediaType.Live ? LiveCapabilities : TimelineCapabilities;
}
