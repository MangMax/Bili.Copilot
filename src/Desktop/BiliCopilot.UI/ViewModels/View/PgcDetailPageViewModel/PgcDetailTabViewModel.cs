// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 番剧详情页分区类型.
/// </summary>
public enum PgcDetailTabKind
{
    /// <summary>
    /// 剧集列表，比如正片、花絮.
    /// </summary>
    Episodes,

    /// <summary>
    /// 简介与演职人员.
    /// </summary>
    Intro,

    /// <summary>
    /// 短评.
    /// </summary>
    ShortReviews,

    /// <summary>
    /// 长评.
    /// </summary>
    LongReviews,
}

/// <summary>
/// 番剧详情页的一个分区，例如正片、花絮、简介、评价.
/// </summary>
public sealed partial class PgcDetailTabViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcDetailTabViewModel"/> class.
    /// </summary>
    /// <param name="title">分区标题.</param>
    /// <param name="episodes">分区里的剧集；简介与评价分区为空.</param>
    /// <param name="kind">分区类型.</param>
    public PgcDetailTabViewModel(string title, List<EpisodeItemViewModel> episodes, PgcDetailTabKind kind = PgcDetailTabKind.Episodes)
    {
        Title = title;
        Episodes = episodes;
        Kind = kind;
    }

    /// <summary>
    /// 分区标题.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; }

    /// <summary>
    /// 分区里的剧集.
    /// </summary>
    public List<EpisodeItemViewModel> Episodes { get; }

    /// <summary>
    /// 分区类型.
    /// </summary>
    public PgcDetailTabKind Kind { get; }

    /// <summary>
    /// 是否是简介分区.
    /// </summary>
    public bool IsIntro => Kind == PgcDetailTabKind.Intro;

    /// <summary>
    /// 是否是短评分区.
    /// </summary>
    public bool IsShortReviews => Kind == PgcDetailTabKind.ShortReviews;

    /// <summary>
    /// 是否是长评分区.
    /// </summary>
    public bool IsLongReviews => Kind == PgcDetailTabKind.LongReviews;

    /// <summary>
    /// 是否是评价分区.
    /// </summary>
    public bool IsReviews => Kind is PgcDetailTabKind.ShortReviews or PgcDetailTabKind.LongReviews;

    /// <summary>
    /// 是否有剧集.
    /// </summary>
    public bool HasEpisodes => Episodes.Count > 0;
}
