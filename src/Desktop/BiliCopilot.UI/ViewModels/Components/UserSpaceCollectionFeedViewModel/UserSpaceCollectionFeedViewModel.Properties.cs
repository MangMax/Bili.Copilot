// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.User;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 用户合集的视图模型.
/// </summary>
public sealed partial class UserSpaceCollectionFeedViewModel
{
    private readonly IUserService _userService;
    private readonly ILogger _logger;
    private UserProfile _user;
    private int _page;
    private int _total;
    private bool _preventLoadMore;
    private int _loadGeneration;
    private readonly object _loadLock = new();
    private CancellationTokenSource? _loadCancellation;

    /// <summary>
    /// 合集列表.
    /// </summary>
    public ObservableCollection<CollectionItemViewModel> Items { get; } = new();

    /// <summary>
    /// 是否正在加载.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// 是否正在加载首页.
    /// </summary>
    [ObservableProperty]
    public partial bool IsInitialLoading { get; set; }

    /// <summary>
    /// 是否正在追加下一页.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    /// <summary>
    /// 列表是否为空.
    /// </summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>
    /// 当前加载的代次；页面卸载时用它只取消本页面发起的那一代加载.
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
}
