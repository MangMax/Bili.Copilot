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
    private List<UserMomentDetailViewModel> _sections;

    [ObservableProperty]
    private UserMomentDetailViewModel _selectedSection;

    [ObservableProperty]
    private bool _isCommentsOpened;

    [ObservableProperty]
    private string _userName;

    [ObservableProperty]
    private bool _isFollowed;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isSearchMode;

    [ObservableProperty]
    private string _searchKeyword;

    [ObservableProperty]
    private bool _isSearchEmpty;

    [ObservableProperty]
    private UserCard _card;

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
