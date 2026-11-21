// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.User;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 关注页面视图模型.
/// </summary>
public sealed partial class FollowsPageViewModel
{
    private readonly IRelationshipService _service;
    private readonly ILogger<FollowsPageViewModel> _logger;
    private readonly Dictionary<UserGroup, List<UserItemViewModel>> _userCache = new();
    private readonly Dictionary<UserGroup, int> _offsetCache = new();
    private CancellationTokenSource? _listLoad;
    private int _searchPage;
    private bool _searchHasMore;

    [ObservableProperty]
    public partial bool IsGroupLoading { get; set; }

    [ObservableProperty]
    public partial bool IsUserLoading { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListTitle))]
    public partial UserGroup SelectedGroup { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListTitle))]
    [NotifyPropertyChangedFor(nameof(EmptyDescription))]
    private partial string SearchKeyword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSearchFailed { get; set; }

    public string ListTitle => SearchKeyword.Length == 0
        ? SelectedGroup?.Name ?? string.Empty
        : ResourceToolkit.GetLocalizedString(StringNames.FollowSearchResults);

    public string EmptyDescription => ResourceToolkit.GetLocalizedString(SearchKeyword.Length == 0 ? StringNames.NoFollows : StringNames.NoFollowSearchResults);

    /// <summary>
    /// 分组加载完成.
    /// </summary>
    public event EventHandler GroupInitialized;

    /// <summary>
    /// 用户列表已完成更新.
    /// </summary>
    public event EventHandler UserListUpdated;

    /// <summary>
    /// 分组.
    /// </summary>
    public ObservableCollection<UserGroup> Groups { get; } = new();

    /// <summary>
    /// 显示的用户列表.
    /// </summary>
    public ObservableCollection<UserItemViewModel> Users { get; } = new();
}
