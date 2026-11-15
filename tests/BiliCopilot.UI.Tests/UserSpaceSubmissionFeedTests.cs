// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 投稿分区的视频/专栏按页加载：同一份列表追加，达到总数或空页后停止；
/// 切换目标后旧目标的迟到响应不得写入列表.
/// </summary>
public sealed class UserSpaceSubmissionFeedTests
{
    [Fact]
    public async Task VideoFeedAppendsPagesAndStopsAtTotal()
    {
        var service = new FeedUserService();
        var feed = new UserSpaceVideoFeedViewModel(service, NullLogger.Instance);
        feed.ResetCommand.Execute(User("A"));

        var firstLoad = feed.LoadItemsCommand.ExecuteAsync(null);
        var first = Assert.Single(service.VideoRequests);
        Assert.Equal(0, first.Page);
        Assert.True(feed.IsInitialLoading);
        Assert.False(feed.IsLoadingMore);

        service.CompleteVideo(first, [Video("v1"), Video("v2")], total: 3);
        await firstLoad;

        Assert.Equal(new[] { "v1", "v2" }, feed.Items.Select(p => p.Data.Identifier.Id));
        Assert.False(feed.IsInitialLoading);
        Assert.False(feed.IsLoadingMore);
        Assert.False(feed.IsEmpty);

        var secondLoad = feed.LoadItemsCommand.ExecuteAsync(null);
        var second = service.VideoRequests[1];
        Assert.Equal(1, second.Page);
        Assert.True(feed.IsLoadingMore);
        Assert.False(feed.IsInitialLoading);

        service.CompleteVideo(second, [Video("v3")], total: 3);
        await secondLoad;
        Assert.Equal(3, feed.Items.Count);

        // 已经达到总数，再滚动也不发请求。
        await feed.LoadItemsCommand.ExecuteAsync(null);
        Assert.Equal(2, service.VideoRequests.Count);
    }

    [Fact]
    public async Task ArticleFeedStopsAfterEmptyPage()
    {
        var service = new FeedUserService();
        var feed = new UserSpaceArticleFeedViewModel(service, NullLogger.Instance);
        feed.ResetCommand.Execute(User("A"));

        var firstLoad = feed.LoadItemsCommand.ExecuteAsync(null);
        service.CompleteArticles(Assert.Single(service.ArticleRequests), [Article("a1")]);
        await firstLoad;
        Assert.Single(feed.Items);

        var secondLoad = feed.LoadItemsCommand.ExecuteAsync(null);
        service.CompleteArticles(service.ArticleRequests[1], []);
        await secondLoad;
        Assert.Single(feed.Items);
        Assert.False(feed.IsEmpty);

        await feed.LoadItemsCommand.ExecuteAsync(null);
        Assert.Equal(2, service.ArticleRequests.Count);
    }

    [Fact]
    public async Task ResetForNewTargetDropsLateVideoResponse()
    {
        var service = new FeedUserService();
        var feed = new UserSpaceVideoFeedViewModel(service, NullLogger.Instance);
        feed.ResetCommand.Execute(User("A"));

        var staleLoad = feed.LoadItemsCommand.ExecuteAsync(null);
        var stale = Assert.Single(service.VideoRequests);

        feed.ResetCommand.Execute(User("B"));
        service.CompleteVideo(stale, [Video("a1")], total: 1);
        await staleLoad;

        Assert.Empty(feed.Items);
        Assert.False(feed.IsLoading);
    }

    private static UserProfile User(string id)
        => new(id, $"User {id}", new BiliImage(new Uri($"https://example.invalid/{id}.png")));

    private static VideoInformation Video(string id)
        => new(
            new MediaIdentifier(id, $"Video {id}", new BiliImage(new Uri($"https://example.invalid/{id}.png"))),
            new PublisherProfile(new UserProfile("42")));

    private static ArticleInformation Article(string id)
        => new(new ArticleIdentifier(id, $"Article {id}", "summary", default));

    private sealed class VideoRequest(int page)
    {
        public int Page { get; } = page;

        public TaskCompletionSource<(IReadOnlyList<VideoInformation> Videos, int TotalCount)> Completion { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ArticleRequest(int page)
    {
        public int Page { get; } = page;

        public TaskCompletionSource<IReadOnlyList<ArticleInformation>> Completion { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FeedUserService : IUserService
    {
        public List<VideoRequest> VideoRequests { get; } = new();

        public List<ArticleRequest> ArticleRequests { get; } = new();

        public void CompleteVideo(VideoRequest request, IReadOnlyList<VideoInformation> videos, int total)
            => request.Completion.TrySetResult((videos, total));

        public void CompleteArticles(ArticleRequest request, IReadOnlyList<ArticleInformation> articles)
            => request.Completion.TrySetResult(articles);

        public Task<(IReadOnlyList<VideoInformation> Videos, int TotalCount)> GetUserVideosAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            var request = new VideoRequest(pageNumber);
            VideoRequests.Add(request);
            return request.Completion.Task;
        }

        public Task<IReadOnlyList<ArticleInformation>> GetUserArticlesAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            var request = new ArticleRequest(pageNumber);
            ArticleRequests.Add(request);
            return request.Completion.Task;
        }

        public Task<(IReadOnlyList<AudioInformation> Audios, int TotalCount)> GetUserAudiosAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default)
            => Task.FromResult(((IReadOnlyList<AudioInformation>)[], 0));

        public Task<(IReadOnlyList<UserCollectionInfo> Collections, int TotalCount)> GetUserCollectionInfosAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default)
            => Task.FromResult(((IReadOnlyList<UserCollectionInfo>)[], 0));

        public Task<UserCard> GetUserInformationAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<UserProfile>> BatchGetUsersAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserCard> GetUserSpaceInformationAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
