// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手控制器返回的动作类型.
/// </summary>
public enum SponsorBlockActionKind
{
    /// <summary>
    /// 无需执行任何操作.
    /// </summary>
    None,

    /// <summary>
    /// 跳转到 <see cref="SponsorBlockDecision.TargetPosition"/>.
    /// </summary>
    Skip,

    /// <summary>
    /// 静音当前片段.
    /// </summary>
    Mute,

    /// <summary>
    /// 恢复进入片段之前的静音状态.
    /// </summary>
    RestoreMute,

    /// <summary>
    /// 显示跳过提示.
    /// </summary>
    ShowButton,

    /// <summary>
    /// 隐藏跳过提示.
    /// </summary>
    HideButton,
}
