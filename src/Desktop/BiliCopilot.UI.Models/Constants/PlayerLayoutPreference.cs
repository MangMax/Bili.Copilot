// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Models.Constants;

/// <summary>
/// 打开播放窗口时的初始界面布局偏好.
/// </summary>
public enum PlayerLayoutPreference
{
    /// <summary>
    /// 记忆上次使用的布局.
    /// </summary>
    Remember,

    /// <summary>
    /// 总是使用铺满布局.
    /// </summary>
    Fill,

    /// <summary>
    /// 总是使用默认布局（信息区+侧栏）.
    /// </summary>
    Default,
}
