// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 阅读器视图模型是单例：连续打开不同文章时，第二次点击不能被正在进行的
/// 第一次加载挡掉，旧响应也不能覆盖新文章的标题、作者、正文或加载态。
/// </summary>
public sealed class ArticleReaderLatestWinsTests
{
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

    private static ArticleDetail Detail(ArticleIdentifier identifier)
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
            false,
            false);
    }

    private sealed class Operation : IArticleOperationService
    {
        public Task ToggleLikeAsync(ArticleIdentifier article, bool isLike, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ToggleFavoriteAsync(ArticleIdentifier article, bool isFavorite, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Discovery : IArticleDiscoveryService
    {
        private readonly Dictionary<string, TaskCompletionSource<ArticleDetail?>> _requests = new();

        public void Complete(string id, ArticleDetail detail) => _requests[id].SetResult(detail);

        public Task<ArticleDetail?> GetArticleContentAsync(ArticleIdentifier identifier, CancellationToken cancellationToken = default)
        {
            var request = new TaskCompletionSource<ArticleDetail?>();
            _requests[identifier.Id!] = request;
            return request.Task;
        }

        public Task<IReadOnlyList<Partition>> GetPartitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Dictionary<int, string>> GetHotCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<ArticleInformation>> GetHotArticlesAsync(int categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<(IReadOnlyList<ArticleInformation> RecommendArticles, IReadOnlyList<ArticleInformation>? TopArticles, int NextPageNumber)> GetRecommendArticlesAsync(int pageNumber = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<(IReadOnlyList<ArticleInformation> RecommendArticles, int NextPageNumber)> GetPartitionArticlesAsync(Partition partition, ArticleSortType sortType, int pageNumber = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
