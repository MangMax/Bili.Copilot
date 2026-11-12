// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 直播条目视图模型.
/// </summary>
public sealed partial class LiveItemViewModel
{
    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// 副标题.
    /// </summary>
    public string Subtitle { get; init; }

    /// <summary>
    /// 封面.
    /// </summary>
    public Uri Cover { get; init; }

    /// <summary>
    /// 观看者人数.
    /// </summary>
    public double? ViewerCount { get; init; }

    /// <summary>
    /// 作者.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// 作者头像.
    /// </summary>
    public Uri? Avatar { get; init; }

    /// <summary>
    /// 标签名称.
    /// </summary>
    public string? TagName { get; init; }

    /// <summary>
    /// 是否正在直播.
    /// </summary>
    public bool? IsLiving { get; init; }

    /// <summary>
    /// 收集时间.
    /// </summary>
    public string? CollectRelativeTime { get; init; }

    /// <summary>
    /// 样式.
    /// </summary>
    public LiveCardStyle Style { get; init; }

    /// <summary>
    /// 是否可以进入作者的用户空间.
    /// </summary>
    public bool CanOpenUserSpace => Data.User is not null;

    /// <summary>
    /// 头像缺失时使用的作者名首字.
    /// </summary>
    public string AuthorInitial => string.IsNullOrWhiteSpace(Author)
        ? "?"
        : System.Globalization.StringInfo.GetNextTextElement(Author);

    /// <summary>
    /// 是否有作者头像. 推荐直播流不返回头像，此时不占用卡片空间.
    /// </summary>
    public bool HasAvatar => Avatar is not null;

    /// <summary>
    /// 当前直播间是否在直播中. 推荐流不返回直播状态，此时按直播中处理.
    /// </summary>
    public bool IsLivingNow => IsLiving != false;

    /// <summary>
    /// 是否有分区/副标题标签.
    /// </summary>
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    /// <summary>
    /// 是否有标签名称.
    /// </summary>
    public bool HasTagName => !string.IsNullOrWhiteSpace(TagName);

    /// <summary>
    /// 是否有收集时间.
    /// </summary>
    public bool HasCollectTime => !string.IsNullOrWhiteSpace(CollectRelativeTime);
}
