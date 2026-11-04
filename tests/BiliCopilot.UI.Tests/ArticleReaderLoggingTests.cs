// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.User;

using static BiliCopilot.UI.Tests.ArticleReaderFixture;

namespace BiliCopilot.UI.Tests;

public sealed class ArticleReaderLoggingTests
{
    [Theory]
    [InlineData("1001")]
    [InlineData("cv1001")]
    public async Task CurrentFailureLogsRequestIdentityGenerationAndOriginalException(string id)
    {
        var service = new Discovery();
        var logger = new CapturedLogger();
        var vm = CreateViewModel(service, logger);
        // Advance past the first generation so a hard-coded generation cannot pass.
        var previous = new ArticleIdentifier("999", "Previous", null, null);
        await Complete(vm, service, previous);
        var article = new ArticleIdentifier(id, "Request", null, null);
        vm.InitializeCommand.Execute(article);
        var task = vm.InitializeCommand.ExecutionTask!;
        var generation = vm.CurrentLoadGeneration;
        var error = new InvalidOperationException("content unavailable");
        service.Fail(id, error);
        await task;

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(error, entry.Exception);
        Assert.Equal(id, entry.Fields["ArticleId"]);
        Assert.Equal(generation, entry.Fields["LoadGeneration"]);
        Assert.Equal(new[] { "ArticleId", "LoadGeneration" },
            entry.Fields.Keys.Where(k => k != "{OriginalFormat}").OrderBy(k => k).ToArray());
    }

    [Fact]
    public async Task CancelledLoadDoesNotLogFailure()
    {
        var service = new Discovery(cancelPendingRequests: true);
        var logger = new CapturedLogger();
        var vm = CreateViewModel(service, logger);
        vm.InitializeCommand.Execute(new ArticleIdentifier("1001", "Cancelled", null, null));
        var task = vm.InitializeCommand.ExecutionTask!;
        vm.CancelLoad(vm.CurrentLoadGeneration);
        await task;

        Assert.True(service.TokenFor("1001").IsCancellationRequested);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleFailureDoesNotLogAgainstNewArticle(bool completeNewFirst)
    {
        // Ignore cancellation to deliver a real late exception from the old transport.
        var service = new Discovery();
        var logger = new CapturedLogger();
        var vm = CreateViewModel(service, logger);
        vm.InitializeCommand.Execute(new ArticleIdentifier("1001", "Old", null, null));
        var oldTask = vm.InitializeCommand.ExecutionTask!;
        var current = new ArticleIdentifier("1002", "Current", null, null);
        vm.InitializeCommand.Execute(current);
        var currentTask = vm.InitializeCommand.ExecutionTask!;
        if (completeNewFirst)
        {
            service.Complete("1002", Detail(current));
            await currentTask;
        }

        service.Fail("1001", new InvalidOperationException("late failure"));
        await oldTask;
        Assert.Empty(logger.Entries);
        Assert.Equal(current, vm.Article);
        if (!completeNewFirst)
        {
            // Prove the logger is live and only the current failure is recorded.
            var error = new InvalidOperationException("current failure");
            service.Fail("1002", error);
            await currentTask;
            var entry = Assert.Single(logger.Entries);
            Assert.Equal("1002", entry.Fields["ArticleId"]);
            Assert.Equal(vm.CurrentLoadGeneration, entry.Fields["LoadGeneration"]);
            Assert.Same(error, entry.Exception);
        }
    }

    private static ArticleReaderPageViewModel CreateViewModel(Discovery discovery, CapturedLogger logger)
        => new(discovery, new Operation(), logger, new CommentMainViewModel(), new AIViewModel());

    private static async Task Complete(ArticleReaderPageViewModel vm, Discovery service, ArticleIdentifier article)
    {
        vm.InitializeCommand.Execute(article);
        var task = vm.InitializeCommand.ExecutionTask!;
        service.Complete(article.Id!, Detail(article));
        await task;
    }

    private sealed record Entry(LogLevel Level, Exception? Exception, Dictionary<string, object?> Fields);

    private sealed class CapturedLogger : ILogger<ArticleReaderPageViewModel>
    {
        public List<Entry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(new(logLevel, exception,
                ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(p => p.Key, p => p.Value)));
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

}
