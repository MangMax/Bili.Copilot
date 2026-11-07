// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播卡片左键点击后的目标。
/// </summary>
public enum LiveEntryTarget
{
    /// <summary>
    /// 应用内播放器.
    /// </summary>
    NativePlayer,

    /// <summary>
    /// 系统浏览器.
    /// </summary>
    Browser,
}

/// <summary>
/// 直播入口策略：把"直播时使用网页播放器"设置翻译成点击后的目标。
/// </summary>
public static class LiveEntryPolicy
{
    /// <summary>
    /// 决定左键点击直播卡片后打开的位置。
    /// </summary>
    /// <param name="useWebPlayerWhenLive">设置项 <c>UseWebPlayerWhenLive</c> 的当前值.</param>
    /// <returns>播放目标.</returns>
    public static LiveEntryTarget Decide(bool useWebPlayerWhenLive)
        => useWebPlayerWhenLive ? LiveEntryTarget.Browser : LiveEntryTarget.NativePlayer;
}
