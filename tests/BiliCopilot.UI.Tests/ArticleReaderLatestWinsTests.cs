// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.Article;
using System.Net;
using System.Text;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 阅读器视图模型是单例：连续打开不同文章时，第二次点击不能被正在进行的
/// 第一次加载挡掉，旧响应也不能覆盖新文章的标题、作者、正文或加载态。
/// </summary>
public sealed class ArticleReaderLatestWinsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LikeCommandPreservesUnknownAndTogglesKnownState(bool? initial)
    {
        var discovery = new Discovery();
        var operation = new Operation();
        var vm = new ArticleReaderPageViewModel(discovery, operation,
            NullLogger<ArticleReaderPageViewModel>.Instance, new CommentMainViewModel(), new AIViewModel());
        var article = new ArticleIdentifier("1001", "Article A", null, null);
        vm.InitializeCommand.Execute(article);
        discovery.Complete("1001", Detail(article, initial, null));
        await vm.InitializeCommand.ExecutionTask!;

        await vm.ToggleLikeCommand.ExecuteAsync(null);

        if (initial is null)
        {
            Assert.Empty(operation.Likes);
            Assert.Null(vm.IsLiked);
            Assert.Equal(0, vm.LikeCount);
        }
        else
        {
            Assert.Equal(!initial.Value, Assert.Single(operation.Likes));
            Assert.Equal(!initial.Value, vm.IsLiked);
            Assert.Equal(initial.Value ? -1 : 1, vm.LikeCount);
        }

        Assert.Null(vm.IsFavorited);
        Assert.Equal(0, vm.FavoriteCount);
        Assert.Empty(operation.Favorites);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FavoriteCommandPreservesUnknownAndTogglesKnownState(bool? initial)
    {
        var discovery = new Discovery();
        var operation = new Operation();
        var vm = new ArticleReaderPageViewModel(discovery, operation,
            NullLogger<ArticleReaderPageViewModel>.Instance, new CommentMainViewModel(), new AIViewModel());
        var article = new ArticleIdentifier("1001", "Article A", null, null);
        vm.InitializeCommand.Execute(article);
        discovery.Complete("1001", Detail(article, null, initial));
        await vm.InitializeCommand.ExecutionTask!;

        await vm.ToggleFavoriteCommand.ExecuteAsync(null);

        if (initial is null)
        {
            Assert.Empty(operation.Favorites);
            Assert.Null(vm.IsFavorited);
            Assert.Equal(0, vm.FavoriteCount);
        }
        else
        {
            Assert.Equal(!initial.Value, Assert.Single(operation.Favorites));
            Assert.Equal(!initial.Value, vm.IsFavorited);
            Assert.Equal(initial.Value ? -1 : 1, vm.FavoriteCount);
        }

        Assert.Null(vm.IsLiked);
        Assert.Equal(0, vm.LikeCount);
    }

    [Fact]
    public async Task MissingStatsPreservesBodyAuthorAndUnknownStates()
    {
        var discovery = new Discovery();
        var operation = new Operation();
        var vm = new ArticleReaderPageViewModel(discovery, operation,
            NullLogger<ArticleReaderPageViewModel>.Instance, new CommentMainViewModel(), new AIViewModel());
        var article = new ArticleIdentifier("1001", "Article A", null, null);
        vm.InitializeCommand.Execute(article);
        discovery.Complete("1001", Detail(article, null, null));
        await vm.InitializeCommand.ExecutionTask!;
        await vm.ToggleLikeCommand.ExecuteAsync(null);
        await vm.ToggleFavoriteCommand.ExecuteAsync(null);
        Assert.Equal("<p>1001</p>", vm.Content!.HtmlContent);
        Assert.Equal("Author 1001", vm.Author);
        Assert.False(vm.IsLoading);
        Assert.Null(vm.IsLiked);
        Assert.Null(vm.IsFavorited);
        Assert.Equal(0, vm.LikeCount);
        Assert.Equal(0, vm.FavoriteCount);
        Assert.Empty(operation.Likes);
        Assert.Empty(operation.Favorites);
    }

    [Fact]
    public async Task SecondOpenRunsAndStaleResponseCannotOverwriteIt()
    {
        var service = new Discovery();
        var ai = new AIViewModel();
        var vm = new ArticleReaderPageViewModel(
            service,
            new Operation(),
            NullLogger<ArticleReaderPageViewModel>.Instance,
            new CommentMainViewModel(),
            ai);
        var initialized = 0;
        vm.ArticleInitialized += (_, _) => initialized++;

        var first = new ArticleIdentifier("1001", "Article A", null, null);
        var second = new ArticleIdentifier("1002", "Article B", null, null);

        vm.InitializeCommand.Execute(first);
        var firstTask = vm.InitializeCommand.ExecutionTask!;
        Assert.Equal("Article A", vm.Title);

        // The singleton reader command must start the second open while the
        // first request is still in flight.
        vm.InitializeCommand.Execute(second);
        var secondTask = vm.InitializeCommand.ExecutionTask!;
        Assert.Equal("Article B", vm.Title);
        Assert.Equal(second, vm.Article);

        // A late first response must not replace the second article.
        service.Complete("1001", Detail(first));
        await firstTask;
        Assert.Null(vm.Content);
        Assert.Equal("Article B", vm.Title);
        Assert.Equal(second, vm.Article);
        Assert.True(vm.IsLoading);
        Assert.Equal(0, initialized);
        Assert.Empty(ai.Injected);

        service.Complete("1002", Detail(second));
        await secondTask;

        Assert.Equal("1002", vm.Content!.Identifier.Id);
        Assert.Equal("Author 1002", vm.Author);
        Assert.False(vm.IsLoading);
        Assert.Equal(1, initialized);
        Assert.Equal("1002", Assert.Single(ai.Injected).Identifier.Id);
    }

    [Fact]
    public async Task FailedSecondOpenClearsPreviousAuthorAndStats()
    {
        var service = new Discovery();
        var ai = new AIViewModel();
        var vm = new ArticleReaderPageViewModel(
            service,
            new Operation(),
            NullLogger<ArticleReaderPageViewModel>.Instance,
            new CommentMainViewModel(),
            ai);
        var initialized = 0;
        vm.ArticleInitialized += (_, _) => initialized++;

        var first = new ArticleIdentifier("1001", "Article A", null, null);
        vm.InitializeCommand.Execute(first);
        var firstTask = vm.InitializeCommand.ExecutionTask!;
        service.Complete("1001", Detail(first));
        await firstTask;
        Assert.Equal("Author 1001", vm.Author);

        // A new open must not keep showing the previous article's identity while
        // loading, nor after this load fails.
        var second = new ArticleIdentifier("1002", "Article B", null, null);
        vm.InitializeCommand.Execute(second);
        var secondTask = vm.InitializeCommand.ExecutionTask!;
        Assert.Equal("Article B", vm.Title);
        Assert.True(vm.IsLoading);
        Assert.Null(vm.Author);
        Assert.Null(vm.Avatar);
        Assert.Equal(0, vm.LikeCount);
        Assert.Equal(0, vm.CommentCount);
        Assert.Equal(0, vm.FavoriteCount);

        service.Fail("1002", new InvalidOperationException("transient content state"));
        await secondTask;

        Assert.Null(vm.Content);
        Assert.Null(vm.Author);
        Assert.Null(vm.Avatar);
        Assert.False(vm.IsLoading);
        Assert.Equal(1, initialized);
    }

    [Fact]
    public async Task SecondOpenCancelsTheFirstRequest()
    {
        var service = new Discovery(cancelPendingRequests: true);
        var vm = CreateViewModel(service);

        var first = new ArticleIdentifier("1001", "Article A", null, null);
        var second = new ArticleIdentifier("1002", "Article B", null, null);

        vm.InitializeCommand.Execute(first);
        var firstTask = vm.InitializeCommand.ExecutionTask!;
        Assert.False(service.TokenFor("1001").IsCancellationRequested);

        vm.InitializeCommand.Execute(second);
        var secondTask = vm.InitializeCommand.ExecutionTask!;

        // The first read is cancelled as soon as the second article starts,
        // instead of continuing to read or to fetch its metadata.
        Assert.True(service.TokenFor("1001").IsCancellationRequested);
        await firstTask;
        Assert.Null(vm.Content);
        Assert.True(vm.IsLoading);
        Assert.Equal(2, service.ContentRequests);

        service.Complete("1002", Detail(second));
        await secondTask;

        Assert.Equal("1002", vm.Content!.Identifier.Id);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LeavingReaderCancelsItsCurrentLoad()
    {
        var service = new Discovery(cancelPendingRequests: true);
        var vm = CreateViewModel(service);

        vm.InitializeCommand.Execute(new ArticleIdentifier("1001", "Article A", null, null));
        var task = vm.InitializeCommand.ExecutionTask!;
        var generation = vm.CurrentLoadGeneration;

        vm.CancelLoad(generation);
        await task;

        Assert.True(service.TokenFor("1001").IsCancellationRequested);
        Assert.Null(vm.Content);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task ResponseArrivingAfterLeavingCannotLand()
    {
        // The transport ignores cancellation here, so the late response really
        // is delivered; leaving the reader must still prevent it from landing.
        var service = new Discovery();
        var vm = CreateViewModel(service);
        var article = new ArticleIdentifier("1001", "Article A", null, null);

        vm.InitializeCommand.Execute(article);
        var task = vm.InitializeCommand.ExecutionTask!;
        var generation = vm.CurrentLoadGeneration;

        vm.CancelLoad(generation);
        service.Complete("1001", Detail(article));
        await task;

        Assert.Null(vm.Content);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LateUnloadOfPreviousPageDoesNotCancelNewArticle()
    {
        var service = new Discovery(cancelPendingRequests: true);
        var vm = CreateViewModel(service);

        var first = new ArticleIdentifier("1001", "Article A", null, null);
        var second = new ArticleIdentifier("1002", "Article B", null, null);

        vm.InitializeCommand.Execute(first);
        var firstTask = vm.InitializeCommand.ExecutionTask!;
        var firstGeneration = vm.CurrentLoadGeneration;

        vm.InitializeCommand.Execute(second);
        var secondTask = vm.InitializeCommand.ExecutionTask!;

        // An old page unloading after the new article started must only cancel
        // the load it owned, never the current one.
        vm.CancelLoad(firstGeneration);
        Assert.False(service.TokenFor("1002").IsCancellationRequested);

        service.Complete("1002", Detail(second));
        await secondTask;
        await firstTask;

        Assert.Equal("1002", vm.Content!.Identifier.Id);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task NewArticleCancelsInFlightHttpReadBeforeMetadata()
    {
        var handler = new BlockingArticleHandler();
        using var httpClient = new BiliHttpClient(handler);
        var discovery = new ArticleDiscoveryService(
            httpClient,
            new BiliAuthenticator(null, new EmptyTokenResolver()),
            new EmptyTokenResolver());
        var vm = CreateViewModel(discovery);

        var first = new ArticleIdentifier("1001", "Article A", null, null);
        var second = new ArticleIdentifier("1002", "Article B", null, null);

        vm.InitializeCommand.Execute(first);
        var firstTask = vm.InitializeCommand.ExecutionTask!;
        await handler.FirstContentRequested.WaitAsync(TimeSpan.FromSeconds(10));

        vm.InitializeCommand.Execute(second);
        var secondTask = vm.InitializeCommand.ExecutionTask!;
        await secondTask;
        await firstTask;

        // 旧文章只发出一次正文请求，读到一半即被取消，且从未请求元数据。
        Assert.True(handler.FirstContentCancelled);
        Assert.Equal(1, handler.FirstContentRequests);
        Assert.Equal(0, handler.FirstMetadataRequests);
        Assert.Equal(1, handler.SecondContentRequests);
        Assert.Equal(1, handler.SecondMetadataRequests);
        Assert.Equal("1002", vm.Content!.Identifier.Id);
        Assert.False(vm.IsLoading);
    }

    private static ArticleReaderPageViewModel CreateViewModel(IArticleDiscoveryService discovery)
        => new(
            discovery,
            new Operation(),
            NullLogger<ArticleReaderPageViewModel>.Instance,
            new CommentMainViewModel(),
            new AIViewModel());

    private static ArticleDetail Detail(ArticleIdentifier identifier, bool? liked = false, bool? favorited = false)
    {
        var id = identifier.Id!;
        var user = new UserProfile(id, $"Author {id}", new BiliImage(new Uri($"https://example.invalid/{id}.png")));
        var card = new UserCard(new UserDetailProfile(user), new UserCommunityInformation(id));
        return new ArticleDetail(
            identifier,
            card,
            new ArticleCommunityInformation(id),
            $"<p>{id}</p>",
            DateTimeOffset.UnixEpoch,
            [],
            liked,
            favorited);
    }

    private sealed class Operation : IArticleOperationService
    {
        public List<bool> Likes { get; } = [];
        public List<bool> Favorites { get; } = [];

        public Task ToggleLikeAsync(ArticleIdentifier article, bool isLike, CancellationToken cancellationToken = default)
        {
            Likes.Add(isLike);
            return Task.CompletedTask;
        }

        public Task ToggleFavoriteAsync(ArticleIdentifier article, bool isFavorite, CancellationToken cancellationToken = default)
        {
            Favorites.Add(isFavorite);
            return Task.CompletedTask;
        }
    }

    private sealed class Discovery : IArticleDiscoveryService
    {
        private readonly Dictionary<string, TaskCompletionSource<ArticleDetail?>> _requests = new();
        private readonly Dictionary<string, CancellationToken> _tokens = new();
        private readonly bool _cancelPendingRequests;

        public Discovery(bool cancelPendingRequests = false) => _cancelPendingRequests = cancelPendingRequests;

        public int ContentRequests { get; private set; }

        public CancellationToken TokenFor(string id) => _tokens[id];

        public void Complete(string id, ArticleDetail detail) => _requests[id].TrySetResult(detail);

        public void Fail(string id, Exception error) => _requests[id].TrySetException(error);

        public Task<ArticleDetail?> GetArticleContentAsync(ArticleIdentifier identifier, CancellationToken cancellationToken = default)
        {
            ContentRequests++;
            var request = new TaskCompletionSource<ArticleDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests[identifier.Id!] = request;
            _tokens[identifier.Id!] = cancellationToken;
            if (_cancelPendingRequests && cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => request.TrySetCanceled(cancellationToken));
            }

            return request.Task;
        }

        public Task<IReadOnlyList<Partition>> GetPartitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Dictionary<int, string>> GetHotCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<ArticleInformation>> GetHotArticlesAsync(int categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<(IReadOnlyList<ArticleInformation> RecommendArticles, IReadOnlyList<ArticleInformation>? TopArticles, int NextPageNumber)> GetRecommendArticlesAsync(int pageNumber = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<(IReadOnlyList<ArticleInformation> RecommendArticles, int NextPageNumber)> GetPartitionArticlesAsync(Partition partition, ArticleSortType sortType, int pageNumber = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// Fake transport for the real article client: the first article's content
    /// read blocks until its caller token is cancelled, the second article's
    /// content and metadata succeed. Request counts expose any work that keeps
    /// running after the first article was replaced.
    /// </summary>
    private sealed class BlockingArticleHandler : HttpMessageHandler
    {
        private const string SecondContentState = """
            {"code":0,"data":{"id":1002,"title":"Article B","publish_time":1700000000,"author":{"mid":43,"name":"Author 1002","face":"https://example.invalid/b.png","fans":1,"level":2},"content":"<p>B body</p>","tags":[]}}
            """;

        private const string SecondViewState = """
            {"code":0,"message":"0","ttl":1,"data":{"like":0,"attention":false,"favorite":false,"coin":0,"stats":{"view":1,"favorite":0,"like":0,"reply":0,"share":0,"coin":0},"mid":43,"author_name":"Author 1002"}}
            """;

        private readonly TaskCompletionSource _firstContentRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _firstContentRequests;
        private int _firstMetadataRequests;
        private int _secondContentRequests;
        private int _secondMetadataRequests;
        private int _firstContentCancelled;

        public Task FirstContentRequested => _firstContentRequested.Task;

        public int FirstContentRequests => Volatile.Read(ref _firstContentRequests);

        public int FirstMetadataRequests => Volatile.Read(ref _firstMetadataRequests);

        public int SecondContentRequests => Volatile.Read(ref _secondContentRequests);

        public int SecondMetadataRequests => Volatile.Read(ref _secondMetadataRequests);

        public bool FirstContentCancelled => Volatile.Read(ref _firstContentCancelled) == 1;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var isFirst = uri.Query.Contains("id=1001", StringComparison.Ordinal);
            if (uri.AbsolutePath == "/x/article/view")
            {
                if (isFirst)
                {
                    Interlocked.Increment(ref _firstContentRequests);
                    _firstContentRequested.TrySetResult();
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Exchange(ref _firstContentCancelled, 1);
                        throw;
                    }

                    throw new InvalidOperationException("The first content read must be cancelled.");
                }

                Interlocked.Increment(ref _secondContentRequests);
                return Json(SecondContentState);
            }

            if (uri.AbsolutePath.Contains("article/viewinfo", StringComparison.Ordinal))
            {
                if (isFirst)
                {
                    Interlocked.Increment(ref _firstMetadataRequests);
                }
                else
                {
                    Interlocked.Increment(ref _secondMetadataRequests);
                }

                return Json(SecondViewState);
            }

            throw new InvalidOperationException($"Unexpected request path '{uri.AbsolutePath}'.");
        }

        private static HttpResponseMessage Html(string html)
            => new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

        private static HttpResponseMessage Json(string json)
            => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class EmptyTokenResolver : IBiliTokenResolver
    {
        public BiliToken? GetToken() => null;

        public void SaveToken(BiliToken token)
        {
        }

        public void RemoveToken()
        {
        }
    }
}
