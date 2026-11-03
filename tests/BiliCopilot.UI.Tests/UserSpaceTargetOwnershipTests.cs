// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Bili.Search;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.Search;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 用户空间页是参数化页面而视图模型是单例：先后打开用户 A 和 B 时共用同一份
/// 视图模型与同一对动态分区。新目标必须开启新的加载代次，旧目标的迟到响应
/// 不得写入任何页面状态，也不得挡住新目标的加载。
/// </summary>
public sealed class UserSpaceTargetOwnershipTests : IDisposable
{
    public UserSpaceTargetOwnershipTests()
    {
        NavigationTestServices.Settings.Value = new()
        {
            [SettingNames.LastUserSpaceSectionIsVideo] = true,
        };
        NavigationTestServices.Current.Value = new()
        {
            [typeof(AppViewModel)] = new AppViewModel(),
            [typeof(PinnerViewModel)] = new PinnerViewModel(),
        };
    }

    public void Dispose()
    {
        NavigationTestServices.Settings.Value = null;
        NavigationTestServices.Current.Value = null;
    }

    [Fact]
    public async Task VideoSectionLateResponseAfterTargetSwitchDoesNotLandAndNewTargetLoadsImmediately()
    {
        var moments = new MomentService();
        var harness = Create(moments, new RelationshipService(), new UserService(), new SearchService());
        var page = harness.Page;
        var video = harness.Video;
        var a = User("A");
        var b = User("B");

        page.InitializeCommand.Execute(a);
        var firstLoad = video.InitializeCommand.ExecutionTask!;
        var staleRequest = Assert.Single(moments.Requests);
        Assert.Equal("A", staleRequest.UserId);
        Assert.True(video.IsLoading);

        page.InitializeCommand.Execute(b);
        var secondLoad = video.InitializeCommand.ExecutionTask!;

        // 新目标不能被上一目标的加载态挡住，必须立刻发出自己的请求。
        Assert.Equal(2, moments.Requests.Count);
        var freshRequest = moments.Requests[1];
        Assert.Equal("B", freshRequest.UserId);
        Assert.True(video.IsLoading);

        // A 的响应在切到 B 之后才返回。
        moments.Complete(staleRequest, [Moment("a-1", a)], "a-2", true);
        await firstLoad;

        Assert.Empty(video.Items);
        Assert.True(video.IsLoading);

        moments.Complete(freshRequest, [Moment("b-1", b)], "b-2", true);
        await secondLoad;

        Assert.Equal("b-1", Assert.Single(video.Items).Data.Id);
        Assert.False(video.IsLoading);
        Assert.False(video.IsEmpty);
    }

    [Fact]
    public async Task ComprehensiveSectionLateResponseAfterTargetSwitchDoesNotLand()
    {
        var moments = new MomentService();
        var harness = Create(moments, new RelationshipService(), new UserService(), new SearchService(), videoSelected: false);
        var page = harness.Page;
        var comprehensive = harness.Comprehensive;
        var a = User("A");
        var b = User("B");

        page.InitializeCommand.Execute(a);
        var firstLoad = comprehensive.InitializeCommand.ExecutionTask!;
        var staleRequest = Assert.Single(moments.Requests);
        Assert.False(staleRequest.IsVideo);
        Assert.Equal("A", staleRequest.UserId);

        page.InitializeCommand.Execute(b);
        var secondLoad = comprehensive.InitializeCommand.ExecutionTask!;
        Assert.Equal(2, moments.Requests.Count);
        var freshRequest = moments.Requests[1];
        Assert.Equal("B", freshRequest.UserId);
        Assert.False(freshRequest.IsVideo);

        moments.Complete(staleRequest, [Moment("a-1", a)], "a-2", true);
        await firstLoad;

        Assert.Empty(comprehensive.Items);
        Assert.True(comprehensive.IsLoading);

        moments.Complete(freshRequest, [Moment("b-1", b)], "b-2", true);
        await secondLoad;

        Assert.Equal("b-1", Assert.Single(comprehensive.Items).Data.Id);
        Assert.False(comprehensive.IsLoading);
    }

    [Fact]
    public async Task LateVideoResponseAfterNewTargetLandedDoesNotMovePagingCursor()
    {
        var moments = new MomentService();
        var harness = Create(moments, new RelationshipService(), new UserService(), new SearchService());
        var page = harness.Page;
        var video = harness.Video;
        var a = User("A");
        var b = User("B");

        page.InitializeCommand.Execute(a);
        var firstLoad = video.InitializeCommand.ExecutionTask!;
        var staleRequest = Assert.Single(moments.Requests);

        page.InitializeCommand.Execute(b);
        var secondLoad = video.InitializeCommand.ExecutionTask!;
        var freshRequest = moments.Requests[1];

        moments.Complete(freshRequest, [Moment("b-1", b)], "b-2", true);
        await secondLoad;
        moments.Complete(staleRequest, [Moment("a-1", a)], "a-2", true);
        await firstLoad;

        Assert.Equal("b-1", Assert.Single(video.Items).Data.Id);

        // 分页游标也属于当前目标：下一次加载必须沿用 B 的游标。
        video.LoadItemsCommand.Execute(null);
        Assert.Equal(3, moments.Requests.Count);
        Assert.Equal("b-2", moments.Requests[2].Offset);
    }

    [Fact]
    public async Task ResetClearsLeftoverLoadingStateSoNewTargetCanLoad()
    {
        var moments = new MomentService();
        var harness = Create(moments, new RelationshipService(), new UserService(), new SearchService());
        var video = harness.Video;
        var a = User("A");
        var b = User("B");

        video.ResetCommand.Execute(a);
        video.InitializeCommand.Execute(null);
        var staleLoad = video.InitializeCommand.ExecutionTask!;
        var staleRequest = Assert.Single(moments.Requests);
        Assert.True(video.IsLoading);

        // 重置目标时只清数据不够：加载态必须一起复位，否则新目标一次请求都发不出去。
        video.ResetCommand.Execute(b);
        Assert.False(video.IsLoading);
        Assert.Empty(video.Items);

        video.InitializeCommand.Execute(null);
        var freshLoad = video.InitializeCommand.ExecutionTask!;
        Assert.Equal(2, moments.Requests.Count);
        var freshRequest = moments.Requests[1];
        Assert.Equal("B", freshRequest.UserId);
        Assert.Null(freshRequest.Offset);

        moments.Complete(staleRequest, [Moment("a-1", a)], "a-2", true);
        await staleLoad;
        Assert.Empty(video.Items);
        Assert.True(video.IsLoading);

        moments.Complete(freshRequest, [Moment("b-1", b)], "b-2", true);
        await freshLoad;

        Assert.Equal("b-1", Assert.Single(video.Items).Data.Id);
        Assert.False(video.IsLoading);
    }

    [Fact]
    public async Task SearchResponseAfterTargetSwitchDoesNotAppend()
    {
        var search = new SearchService();
        var harness = Create(new MomentService(), new RelationshipService(), new UserService(), search);
        var page = harness.Page;
        var a = User("A");
        var b = User("B");

        page.InitializeCommand.Execute(a);
        page.SearchCommand.Execute("a-keyword");
        var staleTask = page.LoadMoreSearchResultCommand.ExecutionTask!;
        var staleRequest = Assert.Single(search.Requests);
        Assert.Equal("A", staleRequest.UserId);
        Assert.Equal("a-keyword", staleRequest.Keyword);
        Assert.True(page.IsSearching);

        page.InitializeCommand.Execute(b);
        Assert.False(page.IsSearchMode);
        Assert.False(page.IsSearching);
        Assert.Empty(page.SearchVideos);

        search.Complete(staleRequest, [Video("a-video")], 1, true);
        await staleTask;

        Assert.Empty(page.SearchVideos);
        Assert.False(page.IsSearching);
        Assert.False(page.IsSearchEmpty);

        // B 的搜索也不会被 A 的搜索态挡住：从第一页重新开始，并能落地自己的结果。
        page.SearchCommand.Execute("b-keyword");
        var freshSearch = search.Requests[1];
        Assert.Equal("B", freshSearch.UserId);
        Assert.Equal("b-keyword", freshSearch.Keyword);
        Assert.Equal(0, freshSearch.PageNumber);
        search.Complete(freshSearch, [Video("b-video")], 1, true);
        await page.LoadMoreSearchResultCommand.ExecutionTask!;
        Assert.Equal("b-video", Assert.Single(page.SearchVideos).Data.Identifier.Id);
    }

    [Fact]
    public async Task SearchResponseAfterExitSearchDoesNotAppend()
    {
        var search = new SearchService();
        var harness = Create(new MomentService(), new RelationshipService(), new UserService(), search);
        var page = harness.Page;
        var a = User("A");

        page.InitializeCommand.Execute(a);
        page.SearchCommand.Execute("a-keyword");
        var staleTask = page.LoadMoreSearchResultCommand.ExecutionTask!;
        var staleRequest = Assert.Single(search.Requests);

        page.ExitSearchCommand.Execute(null);
        Assert.False(page.IsSearching);

        search.Complete(staleRequest, [Video("a-video")], 1, true);
        await staleTask;

        Assert.Empty(page.SearchVideos);
        Assert.False(page.IsSearching);
        Assert.False(page.IsSearchEmpty);
    }

    [Fact]
    public async Task LateFollowStateResponseAfterTargetSwitchDoesNotLand()
    {
        var relationships = new RelationshipService();
        var harness = Create(new MomentService(), relationships, new UserService(), new SearchService());
        var page = harness.Page;
        var a = User("A");
        var b = User("B");

        page.InitializeCommand.Execute(a);
        var firstInit = page.InitializeCommand.ExecutionTask!;
        var staleRelation = Assert.Single(relationships.Requests);
        Assert.Equal("A", staleRelation.UserId);

        page.InitializeCommand.Execute(b);
        var secondInit = page.InitializeCommand.ExecutionTask!;
        Assert.Equal(2, relationships.Requests.Count);
        var freshRelation = relationships.Requests[1];
        Assert.Equal("B", freshRelation.UserId);

        relationships.Complete(freshRelation, UserRelationStatus.Unfollow);
        await secondInit;
        Assert.False(page.IsFollowed);

        // A 的关注态不能在 B 的页面上落盘。
        relationships.Complete(staleRelation, UserRelationStatus.Following);
        await firstInit;
        Assert.False(page.IsFollowed);
    }

    [Fact]
    public async Task SwitchingTargetClearsFollowStateBeforeRelationArrives()
    {
        var relationships = new RelationshipService();
        var harness = Create(new MomentService(), relationships, new UserService(), new SearchService());
        var page = harness.Page;
        var a = User("A");
        var b = User("B");

        page.InitializeCommand.Execute(a);
        relationships.Complete(Assert.Single(relationships.Requests), UserRelationStatus.Following);
        await page.InitializeCommand.ExecutionTask!;
        Assert.True(page.IsFollowed);

        // 切到 B 时关系请求仍在途：B 的页面不能先显示 A 的关注态。
        page.InitializeCommand.Execute(b);
        var freshRelation = relationships.Requests[1];
        Assert.Equal("B", freshRelation.UserId);
        Assert.False(page.IsFollowed);
    }

    [Fact]
    public async Task LateUserCardResponseAfterTargetSwitchDoesNotLand()
    {
        var users = new UserService();
        var harness = Create(new MomentService(), new RelationshipService(), users, new SearchService());
        var page = harness.Page;
        var a = User("A");
        var b = User("B");

        page.InitializeCommand.Execute(a);
        page.InitializeUserInformationCommand.Execute(null);
        var staleTask = page.InitializeUserInformationCommand.ExecutionTask!;
        var staleCard = Assert.Single(users.Requests);
        Assert.Equal("A", staleCard.UserId);

        page.InitializeCommand.Execute(b);
        Assert.Null(page.Card);

        users.Complete(staleCard, Card("A"));
        await staleTask;

        Assert.Null(page.Card);
    }

    [Fact]
    public async Task ResponseArrivingAfterLeavingPageCannotLand()
    {
        // 传输层不理会取消，所以迟到的响应确实会送到；离开页面后它仍然不得落盘。
        var moments = new MomentService();
        var harness = Create(moments, new RelationshipService(), new UserService(), new SearchService());
        var page = harness.Page;
        var video = harness.Video;
        var a = User("A");

        page.InitializeCommand.Execute(a);
        var load = video.InitializeCommand.ExecutionTask!;
        var request = Assert.Single(moments.Requests);
        var generation = page.CurrentLoadGeneration;
        Assert.True(generation > 0);

        page.CancelLoad(generation);
        moments.Complete(request, [Moment("a-1", a)], "a-2", true);
        await load;

        Assert.Empty(video.Items);
        Assert.False(video.IsLoading);
    }

    [Fact]
    public async Task LeavingPageCancelsItsInFlightLoads()
    {
        var moments = new MomentService(honorCancellation: true);
        var harness = Create(moments, new RelationshipService(), new UserService(), new SearchService());
        var page = harness.Page;
        var video = harness.Video;

        page.InitializeCommand.Execute(User("A"));
        var request = Assert.Single(moments.Requests);
        Assert.False(request.Token.IsCancellationRequested);

        page.CancelLoad(page.CurrentLoadGeneration);
        Assert.True(request.Token.IsCancellationRequested);

        await video.InitializeCommand.ExecutionTask!;
        Assert.False(video.IsLoading);
        Assert.Empty(video.Items);
    }

    [Fact]
    public async Task PageReArmingKeepsCancellingOnEveryDeactivation()
    {
        var moments = new MomentService(honorCancellation: true);
        var search = new SearchService();
        var harness = Create(moments, new RelationshipService(), new UserService(), search);
        var page = harness.Page;
        var video = harness.Video;
        var a = User("A");

        page.InitializeCommand.Execute(a);
        var firstLoad = video.InitializeCommand.ExecutionTask!;
        var firstRequest = Assert.Single(moments.Requests);
        var firstGeneration = page.CurrentLoadGeneration;

        // 第一次失活：页面记录本代、取消它，并把记录刷新为新的当前代次。
        Assert.True(page.CancelLoad(firstGeneration));
        var rearmedGeneration = page.CurrentLoadGeneration;
        Assert.NotEqual(firstGeneration, rearmedGeneration);
        await firstLoad;
        Assert.False(video.IsLoading);

        // 返回页面后用户继续翻页与搜索：这些请求属于重新武装后的代次。
        video.LoadItemsCommand.Execute(null);
        var pagedRequest = moments.Requests[1];
        page.SearchCommand.Execute("kw");
        var searchRequest = Assert.Single(search.Requests);
        Assert.True(video.IsLoading);
        Assert.True(page.IsSearching);

        // 过期代次不得误伤重新武装后的在途请求。
        Assert.False(page.CancelLoad(firstGeneration));
        Assert.False(pagedRequest.Token.IsCancellationRequested);
        Assert.False(searchRequest.Token.IsCancellationRequested);

        // 第二次失活：用刷新后的代次必须真正取消，而不是退化为空操作。
        Assert.True(page.CancelLoad(rearmedGeneration));
        Assert.True(pagedRequest.Token.IsCancellationRequested);
        Assert.True(searchRequest.Token.IsCancellationRequested);
        Assert.False(video.IsLoading);
        Assert.False(page.IsSearching);

        // 迟到的响应不落盘。
        moments.Complete(pagedRequest, [Moment("a-1", a)], "a-2", true);
        await video.LoadItemsCommand.ExecutionTask!;
        search.Complete(searchRequest, [Video("a-1")], 1, true);
        await page.LoadMoreSearchResultCommand.ExecutionTask!;

        Assert.Empty(video.Items);
        Assert.Empty(page.SearchVideos);
        Assert.False(video.IsLoading);
        Assert.False(page.IsSearching);
    }

    [Fact]
    public async Task StaleCancelLoadDoesNotCancelNewTargetLoad()
    {
        var moments = new MomentService(honorCancellation: true);
        var harness = Create(moments, new RelationshipService(), new UserService(), new SearchService());
        var page = harness.Page;
        var video = harness.Video;
        var b = User("B");

        page.InitializeCommand.Execute(User("A"));
        var staleGeneration = page.CurrentLoadGeneration;

        page.InitializeCommand.Execute(b);
        var freshRequest = moments.Requests[1];
        Assert.Equal("B", freshRequest.UserId);
        Assert.False(freshRequest.Token.IsCancellationRequested);

        // 旧页面记录的代次已经过期：不得误伤之后发起的新加载。
        page.CancelLoad(staleGeneration);
        Assert.False(freshRequest.Token.IsCancellationRequested);

        moments.Complete(freshRequest, [Moment("b-1", b)], "b-2", true);
        await video.InitializeCommand.ExecutionTask!;

        Assert.Equal("b-1", Assert.Single(video.Items).Data.Id);
        Assert.False(video.IsLoading);
    }

    [Fact]
    public async Task LeavingPageCancelsInFlightSearch()
    {
        var search = new SearchService();
        var harness = Create(new MomentService(), new RelationshipService(), new UserService(), search);
        var page = harness.Page;

        page.InitializeCommand.Execute(User("A"));
        page.SearchCommand.Execute("kw");
        var request = Assert.Single(search.Requests);
        Assert.True(page.IsSearching);

        page.CancelLoad(page.CurrentLoadGeneration);
        Assert.False(page.IsSearching);

        search.Complete(request, [Video("v-1")], 1, true);
        await page.LoadMoreSearchResultCommand.ExecutionTask!;

        Assert.Empty(page.SearchVideos);
        Assert.False(page.IsSearching);
        Assert.False(page.IsSearchEmpty);
    }

    [Fact]
    public async Task SingleTargetBrowsePagingRefreshFollowAndSearchStillWork()
    {
        var moments = new MomentService();
        var relationships = new RelationshipService();
        var users = new UserService();
        var search = new SearchService();
        var harness = Create(moments, relationships, users, search);
        var page = harness.Page;
        var video = harness.Video;
        var a = User("A");

        page.InitializeCommand.Execute(a);
        var init = page.InitializeCommand.ExecutionTask!;
        var first = Assert.Single(moments.Requests);
        Assert.Null(first.Offset);
        moments.Complete(first, [Moment("a-1", a), Moment("a-2", a)], "a-2", true);
        await video.InitializeCommand.ExecutionTask!;

        Assert.Equal(new[] { "a-1", "a-2" }, video.Items.Select(item => item.Data.Id));
        Assert.False(video.IsLoading);
        Assert.False(video.IsEmpty);

        video.LoadItemsCommand.Execute(null);
        var second = moments.Requests[1];
        Assert.Equal("a-2", second.Offset);
        moments.Complete(second, [Moment("a-3", a)], "a-3", false);
        await video.LoadItemsCommand.ExecutionTask!;
        Assert.Equal(3, video.Items.Count);

        // 没有更多时不再发请求。
        video.LoadItemsCommand.Execute(null);
        Assert.Equal(2, moments.Requests.Count);

        // 刷新从第一页重新开始。
        video.RefreshCommand.Execute(null);
        var refreshTask = video.RefreshCommand.ExecutionTask!;
        var refreshed = moments.Requests[2];
        Assert.Null(refreshed.Offset);
        moments.Complete(refreshed, [Moment("a-9", a)], "a-2", true);
        await refreshTask;
        Assert.Equal("a-9", Assert.Single(video.Items).Data.Id);

        // 关注 / 取关走同一条目标归属路径。
        relationships.Complete(Assert.Single(relationships.Requests), UserRelationStatus.Following);
        await init;
        Assert.True(page.IsFollowed);
        page.ToggleFollowCommand.Execute(null);
        await page.ToggleFollowCommand.ExecutionTask!;
        Assert.False(page.IsFollowed);
        Assert.Equal("A", Assert.Single(relationships.Unfollows));

        // 搜索：首屏、翻页、退出。
        page.SearchCommand.Execute("kw");
        var searchTask = page.LoadMoreSearchResultCommand.ExecutionTask!;
        var firstPage = Assert.Single(search.Requests);
        Assert.Equal(0, firstPage.PageNumber);
        search.Complete(firstPage, [Video("v-1")], 1, true);
        await searchTask;
        Assert.Single(page.SearchVideos);
        Assert.False(page.IsSearching);
        Assert.False(page.IsSearchEmpty);

        page.LoadMoreSearchResultCommand.Execute(null);
        var nextPage = search.Requests[1];
        Assert.Equal(1, nextPage.PageNumber);
        search.Complete(nextPage, [Video("v-2")], 2, false);
        await page.LoadMoreSearchResultCommand.ExecutionTask!;
        Assert.Equal(2, page.SearchVideos.Count);

        page.ExitSearchCommand.Execute(null);
        Assert.Empty(page.SearchVideos);
        Assert.False(page.IsSearchMode);
    }

    private static Harness Create(
        IMomentDiscoveryService moments,
        IRelationshipService relationships,
        IUserService users,
        ISearchService search,
        bool videoSelected = true)
    {
        NavigationTestServices.Settings.Value![SettingNames.LastUserSpaceSectionIsVideo] = videoSelected;
        var video = new UserMomentDetailViewModel(moments, NullLogger<UserMomentDetailViewModel>.Instance);
        video.SetIsVideo(true);
        var comprehensive = new UserMomentDetailViewModel(moments, NullLogger<UserMomentDetailViewModel>.Instance);
        comprehensive.SetIsVideo(false);
        var page = new UserSpacePageViewModel(
            new CommentMainViewModel(),
            relationships,
            users,
            search,
            NullLogger<UserSpacePageViewModel>.Instance)
        {
            Sections = new List<UserMomentDetailViewModel> { video, comprehensive },
        };
        return new Harness(page, video, comprehensive);
    }

    private static UserProfile User(string id)
        => new(id, $"User {id}", new BiliImage(new Uri($"https://example.invalid/{id}.png")));

    private static MomentInformation Moment(string id, UserProfile user)
        => new(id, user);

    private static VideoInformation Video(string id)
        => new(
            new MediaIdentifier(id, $"Video {id}", new BiliImage(new Uri($"https://example.invalid/{id}.png"))),
            new PublisherProfile(new UserProfile("42")));

    private static UserCard Card(string id)
        => new(new UserDetailProfile(new UserProfile(id, $"User {id}", new BiliImage(new Uri($"https://example.invalid/{id}.png")))), new UserCommunityInformation(id));

    private sealed record Harness(
        UserSpacePageViewModel Page,
        UserMomentDetailViewModel Video,
        UserMomentDetailViewModel Comprehensive);

    private sealed class MomentRequest(string userId, bool isVideo, string? offset, CancellationToken token)
    {
        public string UserId { get; } = userId;

        public bool IsVideo { get; } = isVideo;

        public string? Offset { get; } = offset;

        public CancellationToken Token { get; } = token;

        public TaskCompletionSource<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> Completion { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class MomentService(bool honorCancellation = false) : IMomentDiscoveryService
    {
        public List<MomentRequest> Requests { get; } = new();

        public void Complete(MomentRequest request, IReadOnlyList<MomentInformation> moments, string? offset, bool hasMore)
            => request.Completion.TrySetResult((moments, offset!, hasMore));

        public Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> GetUserVideoMomentsAsync(UserProfile user, string? offset = default, CancellationToken cancellationToken = default)
            => Record(user, true, offset, cancellationToken);

        public Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> GetUserComprehensiveMomentsAsync(UserProfile user, string? offset = default, CancellationToken cancellationToken = default)
            => Record(user, false, offset, cancellationToken);

        public Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> GetUserMomentsAsync(UserProfile user, string? offset = default, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MomentView> GetComprehensiveMomentsAsync(string? offset = default, string? baseline = default, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MomentView> GetVideoMomentsAsync(string? offset = default, string? baseline = default, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> GetMyMomentsAsync(string? offset = default, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> Record(UserProfile user, bool isVideo, string? offset, CancellationToken cancellationToken)
        {
            var request = new MomentRequest(user.Id, isVideo, offset, cancellationToken);
            Requests.Add(request);
            if (honorCancellation && cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => request.Completion.TrySetCanceled(cancellationToken));
            }

            return request.Completion.Task;
        }
    }

    private sealed class RelationRequest(string userId)
    {
        public string UserId { get; } = userId;

        public TaskCompletionSource<UserRelationStatus> Completion { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class RelationshipService : IRelationshipService
    {
        public List<RelationRequest> Requests { get; } = new();

        public List<string> Follows { get; } = new();

        public List<string> Unfollows { get; } = new();

        public void Complete(RelationRequest request, UserRelationStatus status)
            => request.Completion.TrySetResult(status);

        public Task<UserRelationStatus> GetRelationshipAsync(string userId, CancellationToken cancellationToken = default)
        {
            var request = new RelationRequest(userId);
            Requests.Add(request);
            return request.Completion.Task;
        }

        public Task FollowUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            Follows.Add(userId);
            return Task.CompletedTask;
        }

        public Task UnfollowUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            Unfollows.Add(userId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserGroup>> GetMyFollowUserGroupsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<UserCard> Users, int NextPageNumber)> GetMyFollowUserGroupDetailAsync(string groupId, int pageNumber = 0, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<UserCard> Users, int Count, int NextPageNumber)> GetMyFansAsync(int pageNumber = 0, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CardRequest(string userId)
    {
        public string UserId { get; } = userId;

        public TaskCompletionSource<UserCard> Completion { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class UserService : IUserService
    {
        public List<CardRequest> Requests { get; } = new();

        public void Complete(CardRequest request, UserCard card)
            => request.Completion.TrySetResult(card);

        public Task<UserCard> GetUserInformationAsync(string userId, CancellationToken cancellationToken = default)
        {
            var request = new CardRequest(userId);
            Requests.Add(request);
            return request.Completion.Task;
        }

        public Task<IReadOnlyList<UserProfile>> BatchGetUsersAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserCard> GetUserSpaceInformationAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<VideoInformation> Videos, int TotalCount)> GetUserVideosAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ArticleInformation>> GetUserArticlesAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class SearchRequest(string userId, string keyword, int pageNumber, CancellationToken token)
    {
        public string UserId { get; } = userId;

        public string Keyword { get; } = keyword;

        public int PageNumber { get; } = pageNumber;

        public CancellationToken Token { get; } = token;

        public TaskCompletionSource<(IReadOnlyList<VideoInformation>? Videos, int TotalCount, bool HasMore)> Completion { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class SearchService : ISearchService
    {
        public List<SearchRequest> Requests { get; } = new();

        public void Complete(SearchRequest request, IReadOnlyList<VideoInformation>? videos, int total, bool hasMore)
            => request.Completion.TrySetResult((videos, total, hasMore));

        public Task<(IReadOnlyList<VideoInformation>? Videos, int TotalCount, bool HasMore)> SearchUserVideosAsync(string userId, string keyword, int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            var request = new SearchRequest(userId, keyword, pageNumber, cancellationToken);
            Requests.Add(request);
            return request.Completion.Task;
        }

        public Task<IReadOnlyList<HotSearchItem>> GetTotalHotSearchAsync(int count = 30, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SearchRecommendItem>> GetSearchRecommendsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SearchSuggestItem>> GetSearchSuggestsAsync(string keyword, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<VideoInformation> Videos, int? NextPage)> GetComprehensiveSearchResultAsync(string keyword, int? page = default, ComprehensiveSearchSortType sort = ComprehensiveSearchSortType.Default, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<SearchResultItem> Result, string? Offset)> GetPartitionSearchResultAsync(string keyword, SearchPartition partition, string? offset = default, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<VideoInformation>? Videos, int TotalCount, bool HasMore)> SearchHistoryVideosAsync(string keyword, int pageNumber = 0, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
