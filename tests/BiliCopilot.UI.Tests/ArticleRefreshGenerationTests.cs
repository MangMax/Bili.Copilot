// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 刷新必须替换当前列表：刷新前已经发出的分页请求返回后不能追加条目，
/// 也不能覆盖刷新请求写回的分页游标。
/// </summary>
public sealed class ArticleRefreshGenerationTests : IDisposable
{
    private readonly DispatcherQueue _dispatcher = new();

    public ArticleRefreshGenerationTests()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new()
        {
            [typeof(ILogger<ArticlePartitionDetailViewModel>)] = NullLogger<ArticlePartitionDetailViewModel>.Instance,
            [typeof(DispatcherQueue)] = _dispatcher,
        };
    }

    public void Dispose()
    {
        NavigationTestServices.Settings.Value = null;
        NavigationTestServices.Current.Value = null;
    }

    [Fact]
    public async Task RefreshDiscardsPendingPageAndKeepsFreshCursor()
    {
        var service = new Discovery();
        var detail = new ArticlePartitionDetailViewModel(
            new PartitionViewModel(new Partition("_", "Recommend")),
            service);

        detail.InitializeCommand.Execute(null);
        _dispatcher.Drain();
        Assert.Equal(new[] { 0 }, service.RequestedPages);
        Assert.True(detail.IsArticleLoading);

        // Refresh while the first page request is still in flight.
        detail.RefreshCommand.Execute(null);
        _dispatcher.Drain();
        Assert.Equal(new[] { 0, 0 }, service.RequestedPages);

        // The superseded request comes back late; its page must not be applied
        // and its cursor must not overwrite the refresh request's cursor.
        Complete(service.Request(0), Article("stale-page"), 99);
        Assert.Empty(detail.Articles);
        Assert.True(detail.IsArticleLoading);

        Complete(service.Request(1), Article("fresh-page"), 5);
        Assert.Equal("fresh-page", Assert.Single(detail.Articles).Data.Identifier.Id);
        Assert.False(detail.IsArticleLoading);

        // The next page must continue from the refresh result, not the stale one.
        var loadMore = detail.LoadArticlesCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        Assert.Equal(5, Assert.Single(service.RequestedPages.Skip(2)));
        Complete(service.Request(2), Article("next-page"), 6);
        await loadMore;

        Assert.Equal(
            new[] { "fresh-page", "next-page" },
            detail.Articles.Select(p => p.Data.Identifier.Id));
    }

    private void Complete(
        TaskCompletionSource<(IReadOnlyList<ArticleInformation> Articles, IReadOnlyList<ArticleInformation>? Top, int Offset)> request,
        ArticleInformation article,
        int nextPage)
    {
        // Complete on the view model's dispatcher so its continuation is
        // queued and observed deterministically by Drain.
        _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => request.SetResult(([article], null, nextPage)));
        _dispatcher.Drain();
    }

    private static ArticleInformation Article(string id)
        => new(new ArticleIdentifier(id, id, null, null));

    private sealed class Discovery : IArticleDiscoveryService
    {
        private readonly List<TaskCompletionSource<(IReadOnlyList<ArticleInformation>, IReadOnlyList<ArticleInformation>?, int)>> _requests = new();

        public List<int> RequestedPages { get; } = new();

        public TaskCompletionSource<(IReadOnlyList<ArticleInformation>, IReadOnlyList<ArticleInformation>?, int)> Request(int index) => _requests[index];

        public Task<(IReadOnlyList<ArticleInformation> RecommendArticles, IReadOnlyList<ArticleInformation>? TopArticles, int NextPageNumber)> GetRecommendArticlesAsync(int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(pageNumber);
            var request = new TaskCompletionSource<(IReadOnlyList<ArticleInformation>, IReadOnlyList<ArticleInformation>?, int)>();
            _requests.Add(request);
            return request.Task;
        }

        public Task<IReadOnlyList<Partition>> GetPartitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Dictionary<int, string>> GetHotCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<ArticleInformation>> GetHotArticlesAsync(int categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<(IReadOnlyList<ArticleInformation> RecommendArticles, int NextPageNumber)> GetPartitionArticlesAsync(Partition partition, ArticleSortType sortType, int pageNumber = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ArticleDetail?> GetArticleContentAsync(ArticleIdentifier identifier, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
