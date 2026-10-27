// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手片段类别.
/// </summary>
public enum SponsorBlockCategory
{
    /// <summary>
    /// 未知或暂不支持的类别.
    /// </summary>
    Unknown,

    /// <summary>
    /// 赞助广告.
    /// </summary>
    Sponsor,

    /// <summary>
    /// 自我推广.
    /// </summary>
    SelfPromo,

    /// <summary>
    /// 独家内容.
    /// </summary>
    ExclusiveAccess,

    /// <summary>
    /// 互动提示（如三连）.
    /// </summary>
    Interaction,

    /// <summary>
    /// 精彩时刻.
    /// </summary>
    PoiHighlight,

    /// <summary>
    /// 开场动画.
    /// </summary>
    Intro,

    /// <summary>
    /// 片尾动画.
    /// </summary>
    Outro,

    /// <summary>
    /// 预告或回顾.
    /// </summary>
    Preview,

    /// <summary>
    /// 填充内容.
    /// </summary>
    Padding,

    /// <summary>
    /// 无关片段.
    /// </summary>
    Filler,

    /// <summary>
    /// 音乐视频中的非音乐部分.
    /// </summary>
    MusicOfftopic,
}
