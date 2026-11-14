// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 番剧/电影/电视剧详情页视图模型.
/// </summary>
public sealed partial class PgcDetailPageViewModel : ViewModelBase
{
    private readonly IPlayerService _playerService;
    private readonly IEntertainmentDiscoveryService _entertainmentService;
    private readonly ILogger<PgcDetailPageViewModel> _logger;
    private PgcPlayerView? _view;
    private string? _mediaId;
    private bool _reviewsLoaded;
    private int _loadGeneration;
    private CancellationTokenSource? _loadCancellation;

    /// <summary>
    /// 正在加载.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// 加载失败.
    /// </summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    [ObservableProperty]
    public partial string SeasonTitle { get; set; } = string.Empty;

    /// <summary>
    /// 海报.
    /// </summary>
    [ObservableProperty]
    public partial Uri? Cover { get; set; }

    /// <summary>
    /// 别名.
    /// </summary>
    [ObservableProperty]
    public partial string Alias { get; set; } = string.Empty;

    /// <summary>
    /// 简介.
    /// </summary>
    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    /// <summary>
    /// 标签.
    /// </summary>
    [ObservableProperty]
    public partial string Tags { get; set; } = string.Empty;

    /// <summary>
    /// 评分文本.
    /// </summary>
    [ObservableProperty]
    public partial string ScoreText { get; set; } = string.Empty;

    /// <summary>
    /// 评分人数文本.
    /// </summary>
    [ObservableProperty]
    public partial string RatingCountText { get; set; } = string.Empty;

    /// <summary>
    /// 连载进度文本.
    /// </summary>
    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    /// <summary>
    /// 播放量.
    /// </summary>
    [ObservableProperty]
    public partial double? PlayCount { get; set; }

    /// <summary>
    /// 弹幕量.
    /// </summary>
    [ObservableProperty]
    public partial double? DanmakuCount { get; set; }

    /// <summary>
    /// 追番人数.
    /// </summary>
    [ObservableProperty]
    public partial double? FollowCount { get; set; }

    /// <summary>
    /// 是否已追番.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFollowed { get; set; }

    /// <summary>
    /// 分区列表：正片、花絮等附加内容，以及最后的评价分区.
    /// </summary>
    [ObservableProperty]
    public partial List<PgcDetailTabViewModel> Tabs { get; set; } = [];

    /// <summary>
    /// 当前分区.
    /// </summary>
    [ObservableProperty]
    public partial PgcDetailTabViewModel? SelectedTab { get; set; }

    /// <summary>
    /// 当前分区里的剧集.
    /// </summary>
    [ObservableProperty]
    public partial List<EpisodeItemViewModel> Episodes { get; set; } = [];

    /// <summary>
    /// 当前分区是否有剧集.
    /// </summary>
    [ObservableProperty]
    public partial bool HasEpisodes { get; set; }

    /// <summary>
    /// 当前是否显示简介/评价.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowIntro { get; set; }

    /// <summary>
    /// 当前是否显示评价（短评与长评）.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowReviews { get; set; }

    /// <summary>
    /// 正在加载评价.
    /// </summary>
    [ObservableProperty]
    public partial bool IsReviewsLoading { get; set; }

    /// <summary>
    /// 短评.
    /// </summary>
    [ObservableProperty]
    public partial List<PgcReviewItemViewModel> ShortReviews { get; set; } = [];

    /// <summary>
    /// 长评.
    /// </summary>
    [ObservableProperty]
    public partial List<PgcReviewItemViewModel> LongReviews { get; set; } = [];

    /// <summary>
    /// 是否有短评.
    /// </summary>
    [ObservableProperty]
    public partial bool HasShortReviews { get; set; }

    /// <summary>
    /// 是否有长评.
    /// </summary>
    [ObservableProperty]
    public partial bool HasLongReviews { get; set; }

    /// <summary>
    /// 演职人员.
    /// </summary>
    [ObservableProperty]
    public partial List<PgcLaborItemViewModel> LaborItems { get; set; } = [];

    /// <summary>
    /// 是否有演职人员.
    /// </summary>
    [ObservableProperty]
    public partial bool HasLabor { get; set; }
}

/// <summary>
/// 演职人员条目.
/// </summary>
/// <param name="Title">分组标题，比如「角色声优」.</param>
/// <param name="Content">分组内容.</param>
public sealed record PgcLaborItemViewModel(string Title, string Content);

/// <summary>
/// 一条短评或长评.
/// </summary>
/// <param name="AuthorName">作者.</param>
/// <param name="Avatar">头像.</param>
/// <param name="Title">标题，只有长评有.</param>
/// <param name="Content">内容.</param>
/// <param name="ScoreText">评分文本.</param>
/// <param name="LikeText">点赞数文本.</param>
/// <param name="TimeText">时间文本.</param>
/// <param name="Progress">观看进度.</param>
/// <param name="IsShort">是否短评.</param>
public sealed record PgcReviewItemViewModel(
    string AuthorName,
    Uri? Avatar,
    string? Title,
    string Content,
    string ScoreText,
    string LikeText,
    string TimeText,
    string? Progress,
    bool IsShort);
