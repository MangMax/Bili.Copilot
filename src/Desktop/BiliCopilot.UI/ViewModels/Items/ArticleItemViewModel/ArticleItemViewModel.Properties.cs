// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 文章项视图模型.
/// </summary>
public sealed partial class ArticleItemViewModel
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
    /// 作者.
    /// </summary>
    public string Author { get; init; }

    /// <summary>
    /// 作者头像.
    /// </summary>
    public Uri? Avatar { get; init; }

    /// <summary>
    /// 发布时间.
    /// </summary>
    public string? PublishRelativeTime { get; init; }

    /// <summary>
    /// 点赞数.
    /// </summary>
    public double? LikeCount { get; init; }

    /// <summary>
    /// 回复数. 搜索结果把回复数放在点赞位，因此按卡片样式选择数据源.
    /// </summary>
    public double? ReplyCount { get; init; }

    /// <summary>
    /// 阅读数.
    /// </summary>
    public double? ViewCount { get; init; }

    /// <summary>
    /// 所属分区名称.
    /// </summary>
    public string? PartitionName { get; init; }

    /// <summary>
    /// 收集的相对时间.
    /// </summary>
    public string? CollectRelativeTime { get; init; }

    /// <summary>
    /// 卡片样式.
    /// </summary>
    public ArticleCardStyle Style { get; init; }

    /// <summary>
    /// 卡片对应的作者资料，供头像串联准备动画.
    /// </summary>
    public UserProfile? SpaceUser => Data.Publisher;

    /// <summary>
    /// 是否可以进入作者的用户空间.
    /// </summary>
    public bool CanOpenUserSpace => Data.Publisher is not null;

    /// <summary>
    /// 头像缺失时使用的作者名首字.
    /// </summary>
    public string AuthorInitial => string.IsNullOrWhiteSpace(Author)
        ? "?"
        : System.Globalization.StringInfo.GetNextTextElement(Author);

    /// <summary>
    /// 是否有封面. 部分搜索结果不返回封面.
    /// </summary>
    public bool HasCover => Cover is not null;

    /// <summary>
    /// 是否有阅读数.
    /// </summary>
    public bool HasViewCount => ViewCount >= 0;

    /// <summary>
    /// 搜索结果的社区信息把回复数映射到了点赞位，避免把回复数当成点赞显示.
    /// </summary>
    public bool HasLikeCount => Style != ArticleCardStyle.Search && LikeCount >= 0;

    /// <summary>
    /// 是否有回复数.
    /// </summary>
    public bool HasReplyCount => ReplyCount >= 0;

    /// <summary>
    /// 分区与搜索显示发布时间，历史显示收藏时间.
    /// </summary>
    public string? DisplayTime => Style == ArticleCardStyle.History
        ? CollectRelativeTime
        : PublishRelativeTime;

    /// <summary>
    /// 是否有可显示的时间.
    /// </summary>
    public bool HasDisplayTime => !string.IsNullOrWhiteSpace(DisplayTime);

    /// <summary>
    /// 是否有分区标签.
    /// </summary>
    public bool HasPartitionName => !string.IsNullOrWhiteSpace(PartitionName);
}
