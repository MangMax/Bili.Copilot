// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 动态条目视图模型.
/// </summary>
public sealed partial class MomentItemViewModel
{
    private readonly IMomentOperationService _operationService;
    private readonly ILogger<MomentItemViewModel> _logger;
    private readonly Action<MomentItemViewModel> _showMomentAction;

    /// <summary>未过滤的收藏夹列表，仅用于生成 <see cref="FilteredFavoriteFolders"/>.</summary>
    private List<PlayerFavoriteFolderViewModel>? _favoriteFolders;

    [ObservableProperty]
    public partial double? LikeCount { get; set; }

    [ObservableProperty]
    public partial double? CommentCount { get; set; }

    [ObservableProperty]
    public partial bool IsLiked { get; set; }

    [ObservableProperty]
    public partial bool IsPgc { get; set; }

    /// <summary>
    /// 是否已收藏（仅视频/番剧动态有意义）.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFavorited { get; set; }

    /// <summary>
    /// 收藏数；动态接口不返回该字段，打开卡片后按需补取.
    /// </summary>
    [ObservableProperty]
    public partial double? FavoriteCount { get; set; }

    /// <summary>
    /// 按搜索词过滤后的收藏夹列表.
    /// </summary>
    [ObservableProperty]
    public partial List<PlayerFavoriteFolderViewModel>? FilteredFavoriteFolders { get; set; }

    /// <summary>
    /// 收藏夹搜索词.
    /// </summary>
    [ObservableProperty]
    public partial string FavoriteSearchText { get; set; } = string.Empty;

    /// <summary>
    /// 过滤后的收藏夹列表是否为空.
    /// </summary>
    [ObservableProperty]
    public partial bool HasNoFavoriteFolderResults { get; set; }

    /// <summary>
    /// 是否正在加载收藏夹列表.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoadingFavorites { get; set; }

    /// <summary>
    /// 收藏夹列表是否加载失败.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFavoriteFoldersLoadFailed { get; set; }

    /// <summary>
    /// 作者.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// 头像.
    /// </summary>
    public Uri? Avatar { get; init; }

    /// <summary>
    /// 副标题.
    /// </summary>
    public string? Tip { get; init; }

    /// <summary>
    /// 转发来源提示，例如「转发了 @某人」.
    /// </summary>
    public string? ForwardHint { get; init; }

    /// <summary>
    /// 视频封面.
    /// </summary>
    public Uri? VideoCover { get; init; }

    /// <summary>
    /// 视频时长.
    /// </summary>
    public string? VideoDuration { get; init; }

    /// <summary>
    /// 说明.
    /// </summary>
    public EmoteText? Description { get; init; }

    /// <summary>
    /// 视频标题.
    /// </summary>
    public string VideoTitle { get; init; }

    /// <summary>
    /// 是否没有数据.
    /// </summary>
    public bool NoData { get; init; }

    /// <summary>
    /// 内部内容.
    /// </summary>
    public object? InnerContent { get; init; }

    /// <summary>
    /// 卡片样式.
    /// </summary>
    public MomentCardStyle Style { get; init; }

    /// <summary>
    /// 番剧动态所属的季度 Id；非番剧动态时为空.
    /// </summary>
    public string? SeasonId { get; private set; }

    /// <summary>
    /// 卡片作者的用户资料，与导航命令使用同一数据源；供头像串联准备动画.
    /// </summary>
    public UserProfile? UserSpaceProfile => Data.User;

    /// <summary>Pure-video cards expose their dynamic text without changing the grid height.</summary>
    public bool HasDescriptionPreview => Style == MomentCardStyle.Video && !string.IsNullOrWhiteSpace(Description?.Text);

    /// <summary>只有能定位到稿件的动态才显示收藏入口.</summary>
    public bool CanFavorite => GetFavoriteAid() is not null;
}
