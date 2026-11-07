// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.UI;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 单集条目视图模型.
/// </summary>
public sealed partial class EpisodeItemViewModel
{
    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// 封面.
    /// </summary>
    public Uri? Cover { get; init; }

    /// <summary>
    /// 视频时长.
    /// </summary>
    public string? Duration { get; init; }

    /// <summary>
    /// 是否为预告片.
    /// </summary>
    public bool IsPreview { get; init; }

    /// <summary>
    /// 弹幕数.
    /// </summary>
    public double? DanmakuCount { get; init; }

    /// <summary>
    /// 播放数.
    /// </summary>
    public double? PlayCount { get; init; }

    /// <summary>
    /// 评论数.
    /// </summary>
    public double? CommentCount { get; init; }

    /// <summary>
    /// 硬币数.
    /// </summary>
    public double? CoinCount { get; init; }

    /// <summary>
    /// 点赞数.
    /// </summary>
    public double? LikeCount { get; init; }

    /// <summary>
    /// 索引.
    /// </summary>
    public int? Index { get; init; }

    /// <summary>
    /// 高亮文本.
    /// </summary>
    public string? HighlightText { get; init; }

    /// <summary>
    /// 卡片样式.
    /// </summary>
    public EpisodeCardStyle Style { get; init; }

    /// <summary>
    /// 观看权益角标.
    /// </summary>
    public EpisodeAccessTag AccessTag { get; init; }

    /// <summary>
    /// 是否显示权益角标.
    /// </summary>
    public bool HasAccessTag => AccessTag is not EpisodeAccessTag.Unspecified;

    /// <summary>
    /// 是否在紧凑布局里也提示权益（免费集不提示）.
    /// </summary>
    public bool IsAccessTagHighlighted => AccessTag is EpisodeAccessTag.Vip or EpisodeAccessTag.Preview or EpisodeAccessTag.LimitedFree;

    /// <summary>
    /// 角标文本.
    /// </summary>
    public string? AccessTagText => AccessTag switch
    {
        EpisodeAccessTag.Vip => ResourceToolkit.GetLocalizedString(StringNames.EpisodeAccessVip),
        EpisodeAccessTag.LimitedFree => ResourceToolkit.GetLocalizedString(StringNames.EpisodeAccessLimitedFree),
        EpisodeAccessTag.Preview => ResourceToolkit.GetLocalizedString(StringNames.EpisodeAccessPreview),
        EpisodeAccessTag.Free => ResourceToolkit.GetLocalizedString(StringNames.EpisodeAccessFree),
        _ => default,
    };

    /// <summary>
    /// 角标的无障碍名称.
    /// </summary>
    public string? AccessTagAutomationName => AccessTag switch
    {
        EpisodeAccessTag.Vip => ResourceToolkit.GetLocalizedString(StringNames.EpisodeAccessVipAutomation),
        EpisodeAccessTag.LimitedFree => ResourceToolkit.GetLocalizedString(StringNames.EpisodeAccessLimitedFreeAutomation),
        EpisodeAccessTag.Preview => ResourceToolkit.GetLocalizedString(StringNames.EpisodeAccessPreviewAutomation),
        EpisodeAccessTag.Free => ResourceToolkit.GetLocalizedString(StringNames.EpisodeAccessFreeAutomation),
        _ => default,
    };

    /// <summary>
    /// 角标底色.
    /// </summary>
    /// <remarks>
    /// 颜色只是辅助，角标始终带文字，读屏软件朗读 <see cref="AccessTagAutomationName"/>.
    /// </remarks>
    public Color AccessTagColor => AccessTag switch
    {
        EpisodeAccessTag.Vip => Color.FromArgb(255, 0xFB, 0x72, 0x99),
        EpisodeAccessTag.Preview => Color.FromArgb(255, 0x00, 0xA1, 0xD4),
        EpisodeAccessTag.LimitedFree => Color.FromArgb(255, 0xC2, 0x7F, 0x12),
        EpisodeAccessTag.Free => Color.FromArgb(255, 0x5F, 0x5F, 0x5F),
        _ => Color.FromArgb(0, 0, 0, 0),
    };

    /// <summary>
    /// 是否显示原始高亮文本（已被角标覆盖时不重复显示）.
    /// </summary>
    public bool IsHighlightTextVisible => !string.IsNullOrEmpty(HighlightText) && !HasAccessTag;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
