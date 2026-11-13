// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 视频项视图模型.
/// </summary>
public sealed partial class VideoItemViewModel
{
    /// <summary>
    /// 样式.
    /// </summary>
    public VideoCardStyle Style { get; }

    /// <summary>Whether this scene uses the shared grid-feed card.</summary>
    public bool IsFeedCard => Style is VideoCardStyle.Recommend or VideoCardStyle.Hot or VideoCardStyle.Rank
        or VideoCardStyle.Partition or VideoCardStyle.Search or VideoCardStyle.ViewLater or VideoCardStyle.Favorite;

    /// <summary>
    /// 标题.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleTitle))]
    public partial string Title { get; set; }

    public IReadOnlyList<RecommendationFeedbackReason> RecommendationFeedbackOptions
    {
        get
        {
            if (Style != VideoCardStyle.Recommend || Data.RecommendationFeedback is not { } context) return [];
            return context.Options
                .Where(reason => reason.Operation != RecommendationFeedbackOperation.AvoidPublisher
                    || (reason.Mid ?? context.PublisherId) == context.PublisherId)
                .OrderBy(reason => reason.Operation)
                .DistinctBy(reason => reason.Operation)
                .ToArray();
        }
    }

    public bool IsDynamicVideo { get; }

    public string AccessibleTitle => string.IsNullOrWhiteSpace(Title)
        ? ResourceToolkit.GetLocalizedString(IsDynamicVideo ? StringNames.DynamicVideo : StringNames.NoTitleVideo) : Title;

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
    /// 视频时长.
    /// </summary>
    public string? Duration { get; init; }

    /// <summary>
    /// 发布时间.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PopularDisplayTime))]
    public partial string? PublishRelativeTime { get; set; }

    /// <summary>
    /// 作者头像.
    /// </summary>
    [ObservableProperty]
    public partial Uri? Avatar { get; set; }

    /// <summary>
    /// 播放数.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPopularPlayCount))]
    public partial double? PlayCount { get; set; }

    /// <summary>
    /// 弹幕数.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPopularDanmaku))]
    public partial double? DanmakuCount { get; set; }

    /// <summary>Video introduction, separate from the author's dynamic text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionContent))]
    public partial string? Description { get; set; }

    public EmoteText? DescriptionContent => string.IsNullOrEmpty(Description) ? null : new EmoteText(Description, null);

    /// <summary>
    /// 点赞数.
    /// </summary>
    public double? LikeCount { get; set; }

    /// <summary>
    /// 标签名称.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPopularTag))]
    public partial string? TagName { get; set; }

    /// <summary>
    /// 推荐理由.
    /// </summary>
    public string? RecommendReason { get; init; }

    /// <summary>
    /// 收集时间.
    /// </summary>
    public string? CollectTime { get; init; }

    /// <summary>
    /// 进度文本.
    /// </summary>
    public string? ProgressText { get; init; }

    /// <summary>
    /// 用户信息是否有效.
    /// </summary>
    public bool IsUserValid { get; init; }

    /// <summary>
    /// 卡片作者的用户资料，与导航命令使用同一数据源；供头像串联准备动画.
    /// </summary>
    /// <remarks>
    /// 信息流卡片的头像可能由共享元数据补全，而列表返回的用户资料本身没有头像。
    /// 这里用卡片上实际显示的头像补齐导航资料，页头与串联目标才能与源头像一致.
    /// </remarks>
    public UserProfile? UserSpaceProfile
    {
        get
        {
            var user = Data.Publisher?.User;
            if (user is null || user.Avatar is not null || Avatar is null)
            {
                return user;
            }

            return new UserProfile(user.Id, user.Name, new BiliImage(Avatar));
        }
    }

    /// <summary>
    /// 是否可以进入作者的用户空间.
    /// </summary>
    /// <remarks>
    /// 分区视频列表只返回作者名称（存放在 <see cref="Subtitle"/>），不包含用户 ID，
    /// 需要点击时再通过视频详情补全。
    /// </remarks>
    public bool CanOpenUserSpace => IsUserValid || (Style == VideoCardStyle.Partition && !string.IsNullOrWhiteSpace(Subtitle));

    /// <summary>Distinct creators, including the primary uploader once.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuthorDisplayName))]
    public partial int CreatorCount { get; set; }

    [ObservableProperty]
    public partial bool IsCollaborativeSubmission { get; set; }

    [ObservableProperty]
    public partial bool IsChargingExclusive { get; set; }

    public string AuthorDisplayName => CreatorCount > 1
        ? string.Format(ResourceToolkit.GetLocalizedString(StringNames.CollaborativeAuthor), Author, CreatorCount)
        : Author;

    /// <summary>One-based position in the selected ranking; absent outside ranking results.</summary>
    public int? RankPosition { get; init; }

    /// <summary>Display label for a server-ordered ranking item.</summary>
    public string? RankLabel => RankPosition?.ToString("D2", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>Whether the popular card shows a category tag beside the actions.</summary>
    public bool HasPopularTag => !string.IsNullOrWhiteSpace(TagName);

    /// <summary>Recommendation reasons only decorate grid-feed covers.</summary>
    public bool HasPopularRecommendReason => IsFeedCard && !string.IsNullOrWhiteSpace(PopularRecommendReason);

    /// <summary>Relationship reasons belong beside the author, not in the metadata row.</summary>
    public string? PopularRecommendReason => RemoveFollowingLabel(RecommendReason);

    /// <summary>
    /// 服务端会把关注状态拼进推荐理由（如「已关注 | 竖屏」），
    /// 而作者行已经显示关注徽标，这里去掉重复的片段.
    /// </summary>
    private static string? RemoveFollowingLabel(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var segments = reason
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !IsFollowingText(segment))
            .ToList();
        return segments.Count == 0 ? null : string.Join(" | ", segments);
    }

    private static bool HasFollowingLabel(string? reason)
        => !string.IsNullOrWhiteSpace(reason)
            && reason.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(IsFollowingText);

    private static bool IsFollowingText(string? text)
        => string.Equals(text?.Trim(), "已关注", StringComparison.Ordinal)
            || string.Equals(text?.Trim(), ResourceToolkit.GetLocalizedString(StringNames.Followed), StringComparison.Ordinal);

    /// <summary>First text element used while the author's avatar is unavailable.</summary>
    public string AuthorInitial => string.IsNullOrWhiteSpace(Author) ? "?" : System.Globalization.StringInfo.GetNextTextElement(Author);

    /// <summary>Uses collection time in libraries and server publish-time text in feeds.</summary>
    public string? PopularDisplayTime => Style is VideoCardStyle.ViewLater or VideoCardStyle.Favorite
        ? CollectTime
        : PublishRelativeTime ?? (Style == VideoCardStyle.Hot ? Subtitle : null);

    public bool HasPopularPlayCount => PlayCount >= 0;

    public bool HasPopularDanmaku => Style != VideoCardStyle.Rank && DanmakuCount >= 0;

    public bool HasPopularLikes => Style == VideoCardStyle.Rank && LikeCount >= 0;

    /// <summary>Whether the current account follows the author; null means unknown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFollowing))]
    public partial bool? IsAuthorFollowed { get; set; }

    /// <summary>Only a confirmed following relationship is shown as a label.</summary>
    public bool IsFollowing => IsAuthorFollowed is true;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}