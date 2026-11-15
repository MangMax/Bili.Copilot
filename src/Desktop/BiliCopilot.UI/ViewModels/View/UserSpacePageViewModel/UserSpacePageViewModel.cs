// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Search;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.User;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 用户动态页视图模型.
/// </summary>
public sealed partial class UserSpacePageViewModel : ViewModelBase
{
    private const int HomeVideoPreviewCount = 12;
    private const int HomeMomentPreviewCount = 6;

    private ObservableCollection<MomentItemViewModel>? _momentItems;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSpacePageViewModel"/> class.
    /// </summary>
    public UserSpacePageViewModel(
        CommentMainViewModel comment,
        IRelationshipService relationshipService,
        IUserService userService,
        ISearchService searchService,
        ILogger<UserSpacePageViewModel> logger)
    {
        CommentModule = comment;
        _relationshipService = relationshipService;
        _userService = userService;
        _searchService = searchService;
        _logger = logger;
        VideoFeed = new UserSpaceVideoFeedViewModel(userService, logger);
        ArticleFeed = new UserSpaceArticleFeedViewModel(userService, logger);
        AudioFeed = new UserSpaceAudioFeedViewModel(userService, logger);
        CollectionFeed = new UserSpaceCollectionFeedViewModel(userService, logger);
        VideoFeed.FirstPageLoaded += OnVideoFirstPageLoaded;
    }

    /// <summary>
    /// 取消指定代次的加载；页面失活（切走、关闭、被新目标替换）时用它只取消该页面发起的那一代.
    /// </summary>
    /// <param name="generation">页面记录下的加载代次.</param>
    /// <returns>本次代次匹配并真正取消时为 <see langword="true"/>；过期代次不取消任何加载，返回 <see langword="false"/>.</returns>
    public bool CancelLoad(int generation)
    {
        CancellationTokenSource? cancellation = null;
        var cancelled = false;
        lock (_loadLock)
        {
            if (_loadGeneration == generation)
            {
                cancellation = _loadCancellation;
                _loadCancellation = null;

                // 递增代次，使已经在途的旧成功/异常也无法落地.
                _loadGeneration++;
                cancelled = true;
            }
        }

        if (!cancelled)
        {
            // 旧页面记录的代次已经过期，不得误伤之后发起的新加载.
            return false;
        }

        CancelAndDispose(cancellation);

        // 该页面代次同时拥有动态分区、投稿分页与搜索模块的在途加载。
        if (Sections is not null)
        {
            foreach (var section in Sections)
            {
                section.CancelLoad(section.CurrentLoadGeneration);
            }
        }

        VideoFeed.CancelLoad(VideoFeed.CurrentLoadGeneration);
        ArticleFeed.CancelLoad(ArticleFeed.CurrentLoadGeneration);
        AudioFeed.CancelLoad(AudioFeed.CurrentLoadGeneration);
        CollectionFeed.CancelLoad(CollectionFeed.CurrentLoadGeneration);

        InvalidateSearch();
        return true;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InitializeAsync(UserProfile profile)
    {
        // 用户空间是参数化页面、视图模型是单例：每收到一个新的用户参数就开启新的加载代次，
        // 并取消上一代的在途请求，保证旧目标的响应不会落到新目标的页面上。
        var (generation, cancellation) = BeginLoad();
        var userId = profile.Id;
        _profile = profile;

        // 页头头像由导航参数驱动，必须立即通知，不能等用户卡片数据返回.
        OnPropertyChanged(nameof(AvatarUri));

        // 关注态与用户卡片都属于当前目标：新目标开始时先同步清空，
        // 避免上一目标的值一直显示到本次关系请求返回。
        Card = default;
        IsUserInformationLoading = false;
        HasUserInformationError = false;
        IsFollowed = false;
        UserName = profile.Name;

        // 页面会被缓存复用：上一目标打开的评论浮层不能带到新目标上.
        IsCommentsOpened = false;
        if (IsSearchMode)
        {
            ExitSearch();
        }

        HomeVideos.Clear();
        HomeMoments.Clear();
        SubmissionMoments.Clear();

        if (Sections is null)
        {
            var momentSection = this.Get<UserMomentDetailViewModel>();
            momentSection.SetIsVideo(false);
            Sections = [momentSection];
        }

        var section = Sections[0];
        if (MomentSection is null)
        {
            // 主页的「最近动态」与投稿的「动态」都从这份综合动态增量派生；
            // 只在追加时补一行，不整份替换列表，滚动时不会整块重画.
            MomentSection = section;
            MomentSection.PropertyChanged += OnMomentSectionPropertyChanged;
        }

        section.SetShowCommentAction(ShowComment);

        // 重置目标时一并复位动态分区的加载态与代次，上一目标的 IsLoading 不会挡住新目标。
        section.ResetCommand.Execute(profile);
        AttachMomentItems(section.Items);
        SelectedSection = section;
        section.InitializeCommand.Execute(default);

        VideoFeed.ResetCommand.Execute(profile);
        ArticleFeed.ResetCommand.Execute(profile);
        AudioFeed.ResetCommand.Execute(profile);
        CollectionFeed.ResetCommand.Execute(profile);

        EnsureTabs();
        SelectMomentFilter(SelectedMomentFilter ?? MomentFilterTabs[0]);
        SelectTab(Tabs[0]);
        SelectSubmissionTab(SelectedSubmissionTab ?? SubmissionTabs[0]);

        // 主页首屏需要最近投稿，先取第一页；投稿分区打开时直接复用.
        _ = VideoFeed.LoadItemsCommand.ExecuteAsync(default);

        await InitializeRelationAsync(generation, userId, cancellation.Token);
        if (!IsCurrentLoad(generation))
        {
            return;
        }

        Initialized?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 切换顶部分区.
    /// </summary>
    /// <param name="tab">目标分区.</param>
    [RelayCommand]
    private void SelectTab(UserSpaceTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        SelectedTab = tab;
        IsHomeTab = tab.Kind == UserSpaceTabKind.Home;
        IsMomentsTab = tab.Kind == UserSpaceTabKind.Moments;
        IsSubmissionTab = tab.Kind == UserSpaceTabKind.Submission;
        if (IsSubmissionTab)
        {
            SelectSubmissionTab(SelectedSubmissionTab);
        }
    }

    /// <summary>
    /// 切换动态筛选：全部 / 视频.
    /// </summary>
    /// <param name="tab">目标筛选.</param>
    [RelayCommand]
    private void SelectMomentFilter(UserSpaceTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        SelectedMomentFilter = tab;
        IsAllMomentFilter = tab.Kind == UserSpaceTabKind.AllMoments;
        IsVideoMomentFilter = tab.Kind == UserSpaceTabKind.VideoMoments;
    }

    /// <summary>
    /// 切换投稿分区：视频 / 专栏 / 动态.
    /// </summary>
    /// <param name="tab">目标分区.</param>
    [RelayCommand]
    private void SelectSubmissionTab(UserSpaceTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        SelectedSubmissionTab = tab;
        ShowSubmissionVideos = tab.Kind == UserSpaceTabKind.Videos;
        ShowSubmissionArticles = tab.Kind == UserSpaceTabKind.Articles;
        ShowSubmissionMoments = tab.Kind == UserSpaceTabKind.MomentPosts;
        ShowSubmissionAudio = tab.Kind == UserSpaceTabKind.Audio;
        ShowSubmissionCollections = tab.Kind == UserSpaceTabKind.Collections;
        if (ShowSubmissionVideos && VideoFeed.Items.Count == 0)
        {
            _ = VideoFeed.LoadItemsCommand.ExecuteAsync(default);
        }
        else if (ShowSubmissionArticles && ArticleFeed.Items.Count == 0)
        {
            _ = ArticleFeed.LoadItemsCommand.ExecuteAsync(default);
        }
        else if (ShowSubmissionAudio && AudioFeed.Items.Count == 0)
        {
            _ = AudioFeed.LoadItemsCommand.ExecuteAsync(default);
        }
        else if (ShowSubmissionCollections && CollectionFeed.Items.Count == 0)
        {
            _ = CollectionFeed.LoadItemsCommand.ExecuteAsync(default);
        }
    }

    private void EnsureTabs()
    {
        if (Tabs.Count > 0)
        {
            return;
        }

        Tabs =
        [
            new(ResourceToolkit.GetLocalizedString(StringNames.UserSpaceHome), UserSpaceTabKind.Home),
            new(ResourceToolkit.GetLocalizedString(StringNames.DynamicFeed), UserSpaceTabKind.Moments),
            new(ResourceToolkit.GetLocalizedString(StringNames.UserSpaceSubmission), UserSpaceTabKind.Submission),
        ];
        SubmissionTabs =
        [
            new(ResourceToolkit.GetLocalizedString(StringNames.Video), UserSpaceTabKind.Videos),
            new(ResourceToolkit.GetLocalizedString(StringNames.SpecialColumn), UserSpaceTabKind.Articles),
            new(ResourceToolkit.GetLocalizedString(StringNames.DynamicFeed), UserSpaceTabKind.MomentPosts),
            new(ResourceToolkit.GetLocalizedString(StringNames.Audio), UserSpaceTabKind.Audio),
            new(ResourceToolkit.GetLocalizedString(StringNames.UgcSeason), UserSpaceTabKind.Collections),
        ];
        MomentFilterTabs =
        [
            new(ResourceToolkit.GetLocalizedString(StringNames.Total), UserSpaceTabKind.AllMoments),
            new(ResourceToolkit.GetLocalizedString(StringNames.Video), UserSpaceTabKind.VideoMoments),
        ];
    }

    private void AttachMomentItems(IEnumerable<MomentItemViewModel> items)
    {
        if (_momentItems is not null)
        {
            _momentItems.CollectionChanged -= OnMomentItemsChanged;
        }

        _momentItems = MomentSection?.Items;
        HomeMoments.Clear();
        SubmissionMoments.Clear();
        IsSubmissionMomentsEmpty = false;
        if (_momentItems is null)
        {
            return;
        }

        _momentItems.CollectionChanged += OnMomentItemsChanged;
        foreach (var item in items)
        {
            AppendMoment(item);
        }
    }

    private void OnMomentSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (MomentSection is not { } section)
        {
            return;
        }

        if (e.PropertyName == nameof(UserMomentDetailViewModel.Items))
        {
            // 分区重建列表（切换目标、清空）时换成新的集合实例，并重新挂上监听.
            AttachMomentItems(section.Items);
        }
        else if (e.PropertyName == nameof(UserMomentDetailViewModel.IsLoading) && !section.IsLoading)
        {
            // 一次加载结束后才知道图文/视频动态是否为空；加载中不显示空状态，避免闪一下.
            IsSubmissionMomentsEmpty = SubmissionMoments.Count == 0;
        }
    }

    private void OnMomentItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (MomentItemViewModel item in e.NewItems)
            {
                AppendMoment(item);
            }

            return;
        }

        // 清空或重置：两份派生列表一起清空，避免留下上一目标的条目.
        HomeMoments.Clear();
        SubmissionMoments.Clear();
        if (sender is IEnumerable<MomentItemViewModel> items)
        {
            foreach (var item in items)
            {
                AppendMoment(item);
            }
        }
    }

    private void AppendMoment(MomentItemViewModel item)
    {
        // 主页只留最近几条；投稿的「动态」只留图文，视频类在「视频」分区里.
        if (HomeMoments.Count < HomeMomentPreviewCount)
        {
            HomeMoments.Add(item);
        }

        if (item.InnerContent is not VideoItemViewModel)
        {
            SubmissionMoments.Add(item);
            IsSubmissionMomentsEmpty = false;
        }
    }

    private void OnVideoFirstPageLoaded(object? sender, EventArgs e)
    {
        // 主页的最近投稿取首屏快照：之后投稿继续翻页不影响主页.
        HomeVideos.Clear();
        foreach (var video in VideoFeed.Items.Take(HomeVideoPreviewCount))
        {
            HomeVideos.Add(video);
        }
    }

    [RelayCommand]
    private void Search(string text)
    {
        ExitSearch();
        SearchKeyword = text;
        IsSearchMode = true;
        LoadMoreSearchResultCommand.Execute(default);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsSearchMode)
        {
            var keyword = SearchKeyword;
            SearchCommand.Execute(keyword);
            return;
        }

        // 刷新只重取当前分区的内容，其他分区保留已加载的列表.
        if (IsSubmissionTab)
        {
            switch (SelectedSubmissionTab?.Kind)
            {
                case UserSpaceTabKind.Articles:
                    await ArticleFeed.RefreshCommand.ExecuteAsync(default);
                    break;
                case UserSpaceTabKind.Audio:
                    await AudioFeed.RefreshCommand.ExecuteAsync(default);
                    break;
                case UserSpaceTabKind.Collections:
                    await CollectionFeed.RefreshCommand.ExecuteAsync(default);
                    break;
                case UserSpaceTabKind.MomentPosts:
                    if (SelectedSection is not null)
                    {
                        await SelectedSection.RefreshCommand.ExecuteAsync(default);
                    }

                    break;
                case UserSpaceTabKind.Videos:
                default:
                    await VideoFeed.RefreshCommand.ExecuteAsync(default);
                    break;
            }

            return;
        }

        await VideoFeed.RefreshCommand.ExecuteAsync(default);
        if (SelectedSection is not null)
        {
            await SelectedSection.RefreshCommand.ExecuteAsync(default);
        }
    }

    /// <summary>
    /// 滚动到底时按当前分区继续加载：动态、投稿各栏各自分页.
    /// </summary>
    /// <returns>加载任务.</returns>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadMoreAsync()
    {
        if (IsMomentsTab)
        {
            if (IsVideoMomentFilter)
            {
                await VideoFeed.LoadItemsCommand.ExecuteAsync(default);
            }
            else if (SelectedSection is not null)
            {
                await SelectedSection.LoadItemsCommand.ExecuteAsync(default);
            }

            return;
        }

        if (!IsSubmissionTab)
        {
            return;
        }

        switch (SelectedSubmissionTab?.Kind)
        {
            case UserSpaceTabKind.Articles:
                await ArticleFeed.LoadItemsCommand.ExecuteAsync(default);
                break;
            case UserSpaceTabKind.Audio:
                await AudioFeed.LoadItemsCommand.ExecuteAsync(default);
                break;
            case UserSpaceTabKind.Collections:
                await CollectionFeed.LoadItemsCommand.ExecuteAsync(default);
                break;
            case UserSpaceTabKind.MomentPosts:
                if (SelectedSection is not null)
                {
                    await SelectedSection.LoadItemsCommand.ExecuteAsync(default);
                }

                break;
            case UserSpaceTabKind.Videos:
            default:
                await VideoFeed.LoadItemsCommand.ExecuteAsync(default);
                break;
        }
    }

    [RelayCommand]
    private void SelectSection(UserMomentDetailViewModel section)
    {
        if (section is null || section == SelectedSection)
        {
            return;
        }

        SelectedSection = section;
        SettingsToolkit.WriteLocalSetting(SettingNames.LastUserSpaceSectionIsVideo, SelectedSection.IsVideo());
        SelectedSection.InitializeCommand.Execute(default);
    }

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        var generation = CurrentLoadGeneration;
        var userId = _profile.Id;
        var cancellationToken = CurrentLoadToken;
        if (IsFollowed)
        {
            try
            {
                await _relationshipService.UnfollowUserAsync(userId, cancellationToken);
                if (!IsCurrentLoad(generation))
                {
                    return;
                }

                IsFollowed = false;
                this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.Unfollowed), InfoType.Success));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 目标已切换，结果作废.
            }
            catch (Exception ex)
            {
                if (!IsCurrentLoad(generation))
                {
                    return;
                }

                _logger.LogError(ex, "取消关注用户时失败");
                this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToUnfollowUser), InfoType.Error));
            }
        }
        else
        {
            try
            {
                await _relationshipService.FollowUserAsync(userId, cancellationToken);
                if (!IsCurrentLoad(generation))
                {
                    return;
                }

                IsFollowed = true;
                this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.Followed), InfoType.Success));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 目标已切换，结果作废.
            }
            catch (Exception ex)
            {
                if (!IsCurrentLoad(generation))
                {
                    return;
                }

                _logger.LogError(ex, "关注用户时失败");
                this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToFollowUser), InfoType.Error));
            }
        }
    }

    [RelayCommand]
    private void Pin()
    {
        var pinItem = new PinItem(_profile.Id, _profile.Name, _profile.Avatar.Uri.ToString(), PinContentType.User);
        this.Get<PinnerViewModel>().AddItemCommand.Execute(pinItem);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InitializeUserInformationAsync()
    {
        if (_profile is null || IsUserInformationLoading)
        {
            return;
        }

        // 卡片属于当前目标：记录代次与用户 Id，迟到的响应直接丢弃。
        var generation = CurrentLoadGeneration;
        var userId = _profile.Id;
        var cancellationToken = CurrentLoadToken;
        IsUserInformationLoading = true;
        HasUserInformationError = false;
        try
        {
            var card = await _userService.GetUserInformationAsync(userId, cancellationToken);
            if (!IsCurrentLoad(generation))
            {
                return;
            }

            Card ??= card;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 目标已切换，结果作废.
        }
        catch (Exception ex)
        {
            if (!IsCurrentLoad(generation))
            {
                return;
            }

            _logger.LogError(ex, "获取用户信息时失败");
            HasUserInformationError = true;
        }
        finally
        {
            if (IsCurrentLoad(generation))
            {
                IsUserInformationLoading = false;
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadMoreSearchResultAsync()
    {
        if (_preventLoadMoreSearch || IsSearching || !IsSearchMode || string.IsNullOrEmpty(SearchKeyword) || _profile is null)
        {
            return;
        }

        // 搜索也带代次：退出搜索或切换目标后，迟到的结果不再追加、不再改加载态。
        var (generation, cancellation) = BeginSearch();
        var keyword = SearchKeyword;
        var userId = _profile.Id;
        try
        {
            IsSearching = true;
            var (videos, total, hasMore) = await _searchService.SearchUserVideosAsync(userId, keyword, _searchPn, cancellation.Token);
            if (!IsCurrentSearch(generation))
            {
                return;
            }

            _preventLoadMoreSearch = !hasMore || videos is null || videos.Count == 0;
            if (videos is not null)
            {
                foreach (var item in videos)
                {
                    SearchVideos.Add(new VideoItemViewModel(item, VideoCardStyle.Search));
                }

                SearchUpdated?.Invoke(this, EventArgs.Empty);
            }

            if (!_preventLoadMoreSearch)
            {
                _searchPn++;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Do nothing.
        }
        catch (Exception ex)
        {
            if (!IsCurrentSearch(generation))
            {
                return;
            }

            _logger.LogError(ex, "搜索用户视频时失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToSearchUserVideos), InfoType.Error));
        }
        finally
        {
            cancellation.Dispose();
            lock (_searchLock)
            {
                // 已释放的令牌不再留在字段里，避免后续取消依赖 ObjectDisposedException 兜底。
                if (ReferenceEquals(_searchCancellation, cancellation))
                {
                    _searchCancellation = null;
                }
            }

            if (IsCurrentSearch(generation))
            {
                IsSearchEmpty = SearchVideos.Count == 0;
                IsSearching = false;
            }
        }
    }

    [RelayCommand]
    private void ExitSearch()
    {
        // 退出搜索即结束当前搜索代次，并复位搜索加载态，避免旧搜索挡住新目标。
        InvalidateSearch();
        IsSearchMode = false;
        SearchKeyword = string.Empty;
        IsSearchEmpty = false;
        SearchVideos.Clear();
        _preventLoadMoreSearch = false;
        _searchPn = 0;
    }

    private async Task InitializeRelationAsync(int generation, string userId, CancellationToken cancellationToken)
    {
        try
        {
            var state = await _relationshipService.GetRelationshipAsync(userId, cancellationToken);
            if (!IsCurrentLoad(generation))
            {
                return;
            }

            IsFollowed = state != UserRelationStatus.Unknown && state != UserRelationStatus.Unfollow;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 目标已切换，结果作废.
        }
        catch (Exception ex)
        {
            if (!IsCurrentLoad(generation))
            {
                return;
            }

            _logger.LogError(ex, "获取用户关系时失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToGetUserRelation), InfoType.Error));
        }
    }

    private void ShowComment(MomentItemViewModel data)
    {
        var moment = data.Data;
        if (CommentModule.Id == moment.CommentId)
        {
            return;
        }

        IsCommentsOpened = true;
        CommentModule.Initialize(moment.CommentId, moment.CommentType!.Value, Richasy.BiliKernel.Models.CommentSortType.Hot);
        CommentModule.RefreshCommand.Execute(default);
    }

    private (int Generation, CancellationTokenSource Cancellation) BeginLoad()
    {
        var cancellation = new CancellationTokenSource();
        CancellationTokenSource? previous;
        int generation;
        lock (_loadLock)
        {
            generation = ++_loadGeneration;
            previous = _loadCancellation;
            _loadCancellation = cancellation;
        }

        // 先替换当前代次，再在锁外取消旧请求，避免取消回调重入时持锁。
        CancelAndDispose(previous);
        return (generation, cancellation);
    }

    private bool IsCurrentLoad(int generation)
    {
        lock (_loadLock)
        {
            return generation == _loadGeneration;
        }
    }

    private (int Generation, CancellationTokenSource Cancellation) BeginSearch()
    {
        var cancellation = new CancellationTokenSource();
        CancellationTokenSource? previous;
        int generation;
        lock (_searchLock)
        {
            generation = ++_searchGeneration;
            previous = _searchCancellation;
            _searchCancellation = cancellation;
        }

        CancelAndDispose(previous);
        return (generation, cancellation);
    }

    private bool IsCurrentSearch(int generation)
    {
        lock (_searchLock)
        {
            return generation == _searchGeneration;
        }
    }

    private void InvalidateSearch()
    {
        CancellationTokenSource? previous;
        lock (_searchLock)
        {
            _searchGeneration++;
            previous = _searchCancellation;
            _searchCancellation = null;
        }

        CancelAndDispose(previous);
        IsSearching = false;
    }

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 旧搜索已自行释放，取消是幂等的.
        }
        finally
        {
            cancellation.Dispose();
        }
    }
}
