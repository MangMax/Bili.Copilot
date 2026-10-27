// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手针对某一类片段的行为配置.
/// </summary>
public enum SponsorBlockBehavior
{
    /// <summary>
    /// 不处理：不跳转、不静音、不提示.
    /// </summary>
    None,

    /// <summary>
    /// 自动跳过：到达片段起点时跳到片段终点.
    /// </summary>
    Skip,

    /// <summary>
    /// 仅静音：片段播放期间静音，离开后恢复用户原本的静音状态.
    /// </summary>
    Mute,

    /// <summary>
    /// 显示跳过按钮：片段播放期间显示可点击的提示.
    /// </summary>
    ShowButton,
}
