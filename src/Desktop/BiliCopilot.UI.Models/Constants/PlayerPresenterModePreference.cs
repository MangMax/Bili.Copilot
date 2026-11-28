// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Models.Constants;

/// <summary>
/// 打开播放窗口时的初始播放模式偏好.
/// </summary>
public enum PlayerPresenterModePreference
{
    /// <summary>
    /// 记忆上次使用的播放模式.
    /// </summary>
    Remember,

    /// <summary>
    /// 总是以普通窗口打开.
    /// </summary>
    Normal,

    /// <summary>
    /// 总是以全屏打开.
    /// </summary>
    FullScreen,

    /// <summary>
    /// 总是以小窗（紧凑覆盖）打开.
    /// </summary>
    CompactOverlay,
}
