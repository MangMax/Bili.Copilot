// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Search;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.User;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 用户动态页视图模型.
/// </summary>
public sealed partial class UserSpacePageViewModel
{
    private readonly IRelationshipService _relationshipService;
    private readonly IUserService _userService;
    private readonly ISearchService _searchService;
    private readonly ILogger<UserSpacePageViewModel> _logger;
    private UserProfile _profile;
    private int _searchPn;
    private bool _preventLoadMoreSearch;

    /// <summary>
    /// 目标加载代次，每次收到新的用户参数递增.
    /// </summary>
    /// <remarks>
    /// 用户空间页是参数化页面，视图模型为单例。旧目标的迟到响应不得写入
    /// 关注态、用户卡片或搜索状态。
    /// </remarks>
    private int _loadGeneration;

    private readonly object _loadLock = new();

    private CancellationTokenSource? _loadCancellation;

    /// <summary>
    /// 搜索加载代次，每次发起搜索或退出搜索递增.
    /// </summary>
    private int _searchGeneration;

    private readonly object _searchLock = new();

    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty]
    public partial List<UserMomentDetailViewModel> Sections { get; set; }

    [ObservableProperty]
    public partial UserMomentDetailViewModel SelectedSection { get; set; }

    [ObservableProperty]
    public partial bool IsCommentsOpened { get; set; }

    [ObservableProperty]
    public partial string UserName { get; set; }

    [ObservableProperty]
    public partial bool IsFollowed { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial bool IsSearchMode { get; set; }

    [ObservableProperty]
    public partial string SearchKeyword { get; set; }

    [ObservableProperty]
    public partial bool IsSearchEmpty { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvatarUri))]
    public partial UserCard Card { get; set; }

    /// <summary>
    /// 页头头像地址.
    /// </summary>
    /// <remarks>
    /// 页面生命周期内始终优先使用导航参数里的头像：它就是用户点击的那张图，
    /// 与卡片共用同一份 CDN 地址与磁盘缓存，既能让页头立即显示、避免闪变，
    /// 也能让串联动画的目标保持稳定。卡片数据仍提供其余资料字段.
    /// </remarks>
    public Uri? AvatarUri => _profile?.Avatar?.Uri ?? Card?.Profile?.User.Avatar?.Uri;

    [ObservableProperty]
    public partial bool IsUserInformationLoading { get; set; }

    [ObservableProperty]
    public partial bool HasUserInformationError { get; set; }

    /// <summary>
    /// 区块初始化完成.
    /// </summary>
    public event EventHandler Initialized;

    /// <summary>
    /// 搜索更新.
    /// </summary>
    public event EventHandler SearchUpdated;

    /// <summary>
    /// 评论模块.
    /// </summary>
    public CommentMainViewModel CommentModule { get; }

    /// <summary>
    /// 搜索视频结果.
    /// </summary>
    public ObservableCollection<VideoItemViewModel> SearchVideos { get; } = new();

    /// <summary>
    /// 当前目标加载代次；页面卸载时用它只取消本页面发起的那一代加载.
    /// </summary>
    public int CurrentLoadGeneration
    {
        get
        {
            lock (_loadLock)
            {
                return _loadGeneration;
            }
        }
    }

    private CancellationToken CurrentLoadToken
    {
        get
        {
            lock (_loadLock)
            {
                if (_loadCancellation is null)
                {
                    return CancellationToken.None;
                }

                try
                {
                    return _loadCancellation.Token;
                }
                catch (ObjectDisposedException)
                {
                    return CancellationToken.None;
                }
            }
        }
    }
}
